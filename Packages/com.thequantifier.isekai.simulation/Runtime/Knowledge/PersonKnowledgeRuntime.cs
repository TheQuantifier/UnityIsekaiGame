using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Knowledge
{
    [DisallowMultipleComponent]
    public sealed class PersonKnowledgeRuntime : MonoBehaviour
    {
        [SerializeField] private string personId;
        [SerializeField] private string currentActorId;
        [SerializeField] private string currentBodyId;

        private DefinitionRegistry registry;
        private readonly Dictionary<string, KnowledgeBeliefRecordData> beliefsById = new Dictionary<string, KnowledgeBeliefRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> beliefIdByProposition = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, KnowledgeEvidenceRecordData> evidenceById = new Dictionary<string, KnowledgeEvidenceRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, KnowledgeProcessedTransactionData> processedTransactions = new Dictionary<string, KnowledgeProcessedTransactionData>(StringComparer.Ordinal);
        private readonly List<string> diagnostics = new List<string>();
        private bool suppressEvents;

        public event Action<PersonKnowledgeRuntime, KnowledgeOperationResult> KnowledgeChanged;

        public string PersonId => personId ?? string.Empty;
        public string CurrentActorId => currentActorId ?? string.Empty;
        public string CurrentBodyId => currentBodyId ?? string.Empty;
        public long KnowledgeRevision { get; private set; }
        public KnowledgeReadinessState Readiness { get; private set; } = KnowledgeReadinessState.Uninitialized;
        public bool IsReady => Readiness == KnowledgeReadinessState.Ready;
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();

        private void OnDisable()
        {
            Readiness = KnowledgeReadinessState.Disposed;
        }

        public void Configure(DefinitionRegistry definitionRegistry, string exactPersonId, string actorId = "", string bodyId = "", bool restoring = false)
        {
            registry = definitionRegistry ?? registry;
            personId = exactPersonId ?? string.Empty;
            currentActorId = actorId ?? string.Empty;
            currentBodyId = bodyId ?? string.Empty;
            RecalculateReadiness(restoring);
        }

        public void UpdateCurrentAssociation(string actorId, string bodyId)
        {
            currentActorId = actorId ?? string.Empty;
            currentBodyId = bodyId ?? string.Empty;
            RecalculateReadiness(restoring: false);
        }

        public KnowledgeOperationResult PreviewObservation(KnowledgeObservationRequest request)
        {
            return ApplyObservation(request, preview: true, restoring: false);
        }

        public KnowledgeOperationResult RecordObservation(KnowledgeObservationRequest request, bool restoring = false)
        {
            return ApplyObservation(request, preview: false, restoring);
        }

        public KnowledgeOperationResult ShareBelief(KnowledgeShareRequest request, bool preview = false)
        {
            if (request == null || request.SpeakerBelief == null)
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidRequest, "Knowledge sharing requires a speaker belief.");
            }

            KnowledgeBeliefRecord speakerBelief = request.SpeakerBelief;
            if ((speakerBelief.Definition != null && !speakerBelief.Definition.Shareable || speakerBelief.Data.visibility >= KnowledgeVisibility.Private) && !request.PrivateAccessAuthorized)
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.PrivateFactBlocked, "Knowledge sharing was blocked by visibility policy.", request.TransactionId, preview, KnowledgeRevision);
            }

            KnowledgeObservationRequest observation = new KnowledgeObservationRequest
            {
                PersonId = request.ListenerPersonId,
                TransactionId = request.TransactionId,
                Proposition = speakerBelief.Data.proposition?.Clone(),
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = Mathf.RoundToInt(speakerBelief.Confidence * 0.5f),
                Credibility = request.ListenerCredibility,
                GameTimeSeconds = request.GameTimeSeconds,
                SourceId = request.SpeakerPersonId,
                Visibility = speakerBelief.Data.visibility,
                PrivateAccessAuthorized = request.PrivateAccessAuthorized
            };
            return ApplyObservation(observation, preview, restoring: false);
        }

        public KnowledgeOperationResult MarkStale(string beliefId, string transactionId, string reason)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidRequest, "Stale marking requires a transaction ID.", transactionId, revision: KnowledgeRevision);
            }

            if (processedTransactions.TryGetValue(TransactionKey(transactionId), out KnowledgeProcessedTransactionData processed))
            {
                return DuplicateResult(processed, transactionId);
            }

            if (!beliefsById.TryGetValue(beliefId ?? string.Empty, out KnowledgeBeliefRecordData belief))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.MissingBelief, "Belief is missing.", transactionId, revision: KnowledgeRevision);
            }

            long priorRevision = KnowledgeRevision;
            KnowledgeBeliefRecord prior = WrapBelief(belief);
            if (belief.freshness != KnowledgeFreshnessState.Stale)
            {
                belief.freshness = KnowledgeFreshnessState.Stale;
                belief.lastUpdatedGameTimeSeconds = Math.Max(belief.lastUpdatedGameTimeSeconds, 0d);
                belief.beliefRevision++;
                KnowledgeRevision++;
            }

            KnowledgeBeliefRecord result = WrapBelief(belief);
            RememberTransaction(transactionId, KnowledgeResultCode.Success, belief.beliefId, string.Empty);
            KnowledgeOperationResult operation = KnowledgeOperationResult.Success(string.IsNullOrWhiteSpace(reason) ? "Knowledge marked stale." : reason, transactionId, prior, result, null, null, priorRevision, KnowledgeRevision);
            RaiseChanged(operation);
            return operation;
        }

        public KnowledgeOperationResult ForgetBelief(string beliefId, string transactionId, int confidenceReduction = 250)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidRequest, "Forget request requires a transaction ID.", transactionId, revision: KnowledgeRevision);
            }

            if (processedTransactions.TryGetValue(TransactionKey(transactionId), out KnowledgeProcessedTransactionData processed))
            {
                return DuplicateResult(processed, transactionId);
            }

            if (!beliefsById.TryGetValue(beliefId ?? string.Empty, out KnowledgeBeliefRecordData belief))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.MissingBelief, "Belief is missing.", transactionId, revision: KnowledgeRevision);
            }

            long priorRevision = KnowledgeRevision;
            KnowledgeBeliefRecord prior = WrapBelief(belief);
            belief.confidence = KnowledgeConfidence.Clamp(belief.confidence - Math.Max(0, confidenceReduction));
            belief.forgotten = belief.confidence == 0;
            belief.freshness = belief.forgotten ? KnowledgeFreshnessState.Forgotten : belief.freshness;
            belief.retainedSummary = belief.forgotten ? $"Forgotten summary for {belief.beliefId}" : belief.retainedSummary;
            belief.beliefRevision++;
            KnowledgeRevision++;
            KnowledgeBeliefRecord result = WrapBelief(belief);
            RememberTransaction(transactionId, KnowledgeResultCode.Success, belief.beliefId, string.Empty);
            KnowledgeOperationResult operation = KnowledgeOperationResult.Success("Knowledge forgetting applied.", transactionId, prior, result, null, null, priorRevision, KnowledgeRevision);
            RaiseChanged(operation);
            return operation;
        }

        public KnowledgeSnapshot CreateSnapshot(KnowledgeDomain? domain = null, string subjectId = null, string bodyId = null, KnowledgeBeliefState? state = null)
        {
            IReadOnlyList<KnowledgeBeliefRecord> beliefs = beliefsById.Values
                .Select(WrapBelief)
                .Where(record => record != null)
                .Where(record => domain == null || record.Definition == null || record.Definition.Domain == domain.Value)
                .Where(record => string.IsNullOrWhiteSpace(subjectId) || string.Equals(record.Proposition.SubjectId, subjectId, StringComparison.Ordinal))
                .Where(record => string.IsNullOrWhiteSpace(bodyId) || string.Equals(record.Proposition.BodyContextId, bodyId, StringComparison.Ordinal))
                .Where(record => state == null || record.State == state.Value)
                .OrderBy(record => record.BeliefId, StringComparer.Ordinal)
                .ToArray();

            return new KnowledgeSnapshot(
                PersonId,
                CurrentActorId,
                CurrentBodyId,
                KnowledgeRevision,
                Readiness,
                beliefs,
                evidenceById.Values.Select(data => new KnowledgeEvidenceRecord(data)).OrderBy(record => record.EvidenceId, StringComparer.Ordinal).ToArray(),
                diagnostics);
        }

        public InformationAccessProjection<KnowledgeBeliefRecord> GetKnowledgeProjection(KnowledgePropositionData proposition, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (!TryGetBelief(proposition, out KnowledgeBeliefRecord belief))
            {
                return new InformationAccessProjection<KnowledgeBeliefRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Knowledge belief was not found.");
            }

            string[] detailIds = KnowledgeDetailIds(belief);
            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(
                accessContext,
                BuildKnowledgeSubject(belief),
                InformationAccessMode.Query,
                InformationAccessPurpose.Codex,
                detailIds,
                policyId);
            RedactedInformationProjection projection = accessRuntime?.Project(context, detailIds);
            if (projection == null)
            {
                return new InformationAccessProjection<KnowledgeBeliefRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            if (recordAudit)
            {
                accessRuntime.RecordAudit(projection.Decision, context, gameplayAudit: false);
            }

            KnowledgeBeliefRecord projected = projection.Decision.Denied ? null : MaterializeKnowledgeBelief(belief, projection.Details);
            string visibleSubjectId = projected == null || !InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.belief") ? string.Empty : projected.BeliefId;
            return new InformationAccessProjection<KnowledgeBeliefRecord>(projected, projection.Decision, projection.Details, visibleSubjectId, projection.Decision.VisibleReason);
        }

        public IReadOnlyList<InformationAccessProjection<KnowledgeBeliefRecord>> QueryKnowledgeProjections(InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, KnowledgeDomain? domain = null, string subjectId = null, string policyId = "")
        {
            return CreateSnapshot(domain, subjectId).Beliefs
                .Select(record => GetKnowledgeProjection(record.Data.proposition, accessRuntime, accessContext, policyId, recordAudit: false))
                .Where(projection => projection.Succeeded)
                .OrderBy(projection => projection.Record == null ? string.Empty : projection.Record.BeliefId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool TryGetBelief(KnowledgePropositionData proposition, out KnowledgeBeliefRecord belief)
        {
            belief = null;
            string propositionId = KnowledgeProposition.BuildIdentity(proposition);
            if (string.IsNullOrWhiteSpace(propositionId)
                || !beliefIdByProposition.TryGetValue(propositionId, out string beliefId)
                || !beliefsById.TryGetValue(beliefId, out KnowledgeBeliefRecordData data))
            {
                return false;
            }

            belief = WrapBelief(data);
            return true;
        }

        public bool DoesPersonKnow(KnowledgePropositionData proposition)
        {
            return TryGetBelief(proposition, out KnowledgeBeliefRecord belief) && belief.State == KnowledgeBeliefState.Known;
        }

        public int GetConfidence(KnowledgePropositionData proposition)
        {
            return TryGetBelief(proposition, out KnowledgeBeliefRecord belief) ? belief.Confidence : 0;
        }

        public KnowledgeValidationResult ValidateKnowledge()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(PersonId))
            {
                errors.Add("Knowledge runtime has no Person ID.");
            }

            if (registry == null)
            {
                errors.Add("Knowledge runtime has no Definition registry.");
            }

            HashSet<string> evidenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeEvidenceRecordData evidence in evidenceById.Values)
            {
                if (!evidenceIds.Add(evidence.evidenceId))
                {
                    errors.Add($"Duplicate evidence ID '{evidence.evidenceId}'.");
                }

                if (!string.Equals(evidence.observerPersonId, PersonId, StringComparison.Ordinal))
                {
                    errors.Add($"Evidence '{evidence.evidenceId}' is owned by '{evidence.observerPersonId}' instead of '{PersonId}'.");
                }

                string failure = string.Empty;
                if (!TryResolveFact(evidence.proposition?.factDefinitionId, out KnowledgeFactDefinition definition)
                    || !KnowledgeProposition.Validate(evidence.proposition, definition, out failure))
                {
                    errors.Add($"Evidence '{evidence.evidenceId}' has invalid proposition: {failure}");
                }

                if (evidence.strength < KnowledgeConfidence.Minimum || evidence.strength > KnowledgeConfidence.Maximum)
                {
                    errors.Add($"Evidence '{evidence.evidenceId}' has out-of-range strength.");
                }
            }

            HashSet<string> beliefIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeBeliefRecordData belief in beliefsById.Values)
            {
                if (!beliefIds.Add(belief.beliefId))
                {
                    errors.Add($"Duplicate belief ID '{belief.beliefId}'.");
                }

                if (!string.Equals(belief.personId, PersonId, StringComparison.Ordinal))
                {
                    errors.Add($"Belief '{belief.beliefId}' is owned by '{belief.personId}' instead of '{PersonId}'.");
                }

                string failure = string.Empty;
                if (!TryResolveFact(belief.proposition?.factDefinitionId, out KnowledgeFactDefinition definition)
                    || !KnowledgeProposition.Validate(belief.proposition, definition, out failure))
                {
                    errors.Add($"Belief '{belief.beliefId}' has invalid proposition: {failure}");
                    continue;
                }

                if (belief.confidence < KnowledgeConfidence.Minimum || belief.confidence > KnowledgeConfidence.Maximum)
                {
                    errors.Add($"Belief '{belief.beliefId}' has out-of-range confidence.");
                }

                if (KnowledgeBeliefRecord.DeriveState(belief, definition) == KnowledgeBeliefState.Known
                    && belief.supportingEvidenceIds != null
                    && belief.supportingEvidenceIds.Length < definition.RequiredEvidenceCount)
                {
                    errors.Add($"Belief '{belief.beliefId}' is Known without enough evidence.");
                }

                if (belief.forgotten && belief.freshness != KnowledgeFreshnessState.Forgotten)
                {
                    errors.Add($"Belief '{belief.beliefId}' is forgotten but still active.");
                }

                if (belief.firstLearnedGameTimeSeconds > belief.lastUpdatedGameTimeSeconds)
                {
                    errors.Add($"Belief '{belief.beliefId}' has impossible time ordering.");
                }
            }

            return new KnowledgeValidationResult(errors.Count == 0, errors, warnings);
        }

        public PersonKnowledgeSaveData CreateSaveData()
        {
            return new PersonKnowledgeSaveData
            {
                schemaVersion = PersonKnowledgeSaveData.CurrentSchemaVersion,
                personId = PersonId,
                currentActorId = CurrentActorId,
                currentBodyId = CurrentBodyId,
                knowledgeRevision = KnowledgeRevision,
                beliefs = beliefsById.Values.OrderBy(data => data.beliefId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                evidence = evidenceById.Values.OrderBy(data => data.evidenceId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                processedTransactions = processedTransactions.Values.OrderBy(data => data.transactionId, StringComparer.Ordinal).ToArray()
            };
        }

        public KnowledgeOperationResult RestoreFromSaveData(PersonKnowledgeSaveData saveData, DefinitionRegistry definitionRegistry, string expectedPersonId, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, expectedPersonId, out string failureReason))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.RestoreFailed, failureReason, revision: KnowledgeRevision);
            }

            PersonKnowledgeSaveData rollback = CreateSaveData();
            try
            {
                suppressEvents = restoring;
                Readiness = KnowledgeReadinessState.Restoring;
                registry = definitionRegistry ?? registry;
                personId = saveData.personId;
                currentActorId = saveData.currentActorId;
                currentBodyId = saveData.currentBodyId;
                beliefsById.Clear();
                beliefIdByProposition.Clear();
                evidenceById.Clear();
                processedTransactions.Clear();

                foreach (KnowledgeEvidenceRecordData evidence in saveData.evidence ?? Array.Empty<KnowledgeEvidenceRecordData>())
                {
                    evidenceById[evidence.evidenceId] = evidence.Clone();
                }

                foreach (KnowledgeBeliefRecordData belief in saveData.beliefs ?? Array.Empty<KnowledgeBeliefRecordData>())
                {
                    beliefsById[belief.beliefId] = belief.Clone();
                    beliefIdByProposition[KnowledgeProposition.BuildIdentity(belief.proposition)] = belief.beliefId;
                }

                foreach (KnowledgeProcessedTransactionData transaction in saveData.processedTransactions ?? Array.Empty<KnowledgeProcessedTransactionData>())
                {
                    if (!string.IsNullOrWhiteSpace(transaction.transactionId))
                    {
                        processedTransactions[TransactionKey(transaction.transactionId)] = transaction;
                    }
                }

                KnowledgeRevision = Math.Max(0L, saveData.knowledgeRevision);
                RecalculateReadiness(restoring);
                return KnowledgeOperationResult.Success("Person Knowledge restored.", string.Empty, null, null, null, null, KnowledgeRevision, KnowledgeRevision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, rollback.personId, restoring: true);
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.RestoreFailed, exception.Message, revision: KnowledgeRevision);
            }
            finally
            {
                suppressEvents = false;
            }
        }

        public static bool ValidateSaveData(PersonKnowledgeSaveData saveData, DefinitionRegistry definitionRegistry, string expectedPersonId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Knowledge save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != PersonKnowledgeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Knowledge schema version {saveData.schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveData.personId))
            {
                failureReason = "Knowledge save data has no Person ID.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPersonId) && !string.Equals(saveData.personId, expectedPersonId, StringComparison.Ordinal))
            {
                failureReason = $"Knowledge save Person '{saveData.personId}' does not match expected Person '{expectedPersonId}'.";
                return false;
            }

            if (definitionRegistry == null)
            {
                failureReason = "Knowledge restore requires a Definition registry.";
                return false;
            }

            HashSet<string> beliefIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> evidenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeEvidenceRecordData evidence in saveData.evidence ?? Array.Empty<KnowledgeEvidenceRecordData>())
            {
                if (string.IsNullOrWhiteSpace(evidence.evidenceId) || !evidenceIds.Add(evidence.evidenceId))
                {
                    failureReason = $"Knowledge save has missing or duplicate evidence ID '{evidence.evidenceId}'.";
                    return false;
                }

                if (!definitionRegistry.TryGet(evidence.proposition?.factDefinitionId, out KnowledgeFactDefinition definition)
                    || !KnowledgeProposition.Validate(evidence.proposition, definition, out failureReason))
                {
                    return false;
                }
            }

            foreach (KnowledgeBeliefRecordData belief in saveData.beliefs ?? Array.Empty<KnowledgeBeliefRecordData>())
            {
                if (string.IsNullOrWhiteSpace(belief.beliefId) || !beliefIds.Add(belief.beliefId))
                {
                    failureReason = $"Knowledge save has missing or duplicate belief ID '{belief.beliefId}'.";
                    return false;
                }

                if (!definitionRegistry.TryGet(belief.proposition?.factDefinitionId, out KnowledgeFactDefinition definition)
                    || !KnowledgeProposition.Validate(belief.proposition, definition, out failureReason))
                {
                    return false;
                }
            }

            return true;
        }

        private KnowledgeOperationResult ApplyObservation(KnowledgeObservationRequest request, bool preview, bool restoring)
        {
            long priorRevision = KnowledgeRevision;
            if (request == null)
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidRequest, "Knowledge observation request is missing.", revision: KnowledgeRevision);
            }

            if (!string.Equals(request.PersonId, PersonId, StringComparison.Ordinal))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.MissingPerson, $"Observation targets Person '{request.PersonId}', but this runtime owns '{PersonId}'.", request.TransactionId, preview, KnowledgeRevision);
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidRequest, "Knowledge observation requires a transaction ID.", request.TransactionId, preview, KnowledgeRevision);
            }

            if (!preview && processedTransactions.TryGetValue(TransactionKey(request.TransactionId), out KnowledgeProcessedTransactionData processed))
            {
                return DuplicateResult(processed, request.TransactionId);
            }

            if (!TryResolveFact(request.Proposition?.factDefinitionId, out KnowledgeFactDefinition definition))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.MissingFactDefinition, "Knowledge observation references a missing Fact definition.", request.TransactionId, preview, KnowledgeRevision);
            }

            if (!KnowledgeProposition.Validate(request.Proposition, definition, out string failureReason))
            {
                return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidProposition, failureReason, request.TransactionId, preview, KnowledgeRevision);
            }

            if (!VisibilityAllowsAcquisition(request.Visibility, request.PrivateAccessAuthorized, request.HasTruthAuthorization, out KnowledgeResultCode blockedCode, out failureReason))
            {
                return KnowledgeOperationResult.Failure(blockedCode, failureReason, request.TransactionId, preview, KnowledgeRevision);
            }

            string propositionId = KnowledgeProposition.BuildIdentity(request.Proposition);
            KnowledgeBeliefRecordData priorData = beliefIdByProposition.TryGetValue(propositionId, out string beliefId) && beliefsById.TryGetValue(beliefId, out KnowledgeBeliefRecordData found)
                ? found.Clone()
                : null;
            KnowledgeBeliefRecord priorBelief = priorData == null ? null : WrapBelief(priorData);
            string evidenceId = string.IsNullOrWhiteSpace(request.EvidenceId)
                ? StableEvidenceId(PersonId, request.TransactionId, propositionId)
                : request.EvidenceId;
            if (!preview && evidenceById.TryGetValue(evidenceId, out KnowledgeEvidenceRecordData existingEvidence))
            {
                if (!string.Equals(KnowledgeProposition.BuildIdentity(existingEvidence.proposition), propositionId, StringComparison.Ordinal)
                    || existingEvidence.direction != request.Direction
                    || existingEvidence.provenance != request.Provenance)
                {
                    return KnowledgeOperationResult.Failure(KnowledgeResultCode.InvalidEvidence, $"Evidence ID '{evidenceId}' is already used for different evidence.", request.TransactionId, preview, KnowledgeRevision);
                }

                KnowledgeBeliefRecord existingBelief = priorBelief;
                KnowledgeEvidenceRecord existingEvidenceRecord = new KnowledgeEvidenceRecord(existingEvidence);
                RememberTransaction(request.TransactionId, KnowledgeResultCode.Duplicate, existingBelief?.BeliefId ?? string.Empty, evidenceId);
                return KnowledgeOperationResult.Success("Duplicate Knowledge evidence ignored.", request.TransactionId, existingBelief, existingBelief, existingEvidenceRecord, null, KnowledgeRevision, KnowledgeRevision, duplicate: true);
            }

            KnowledgeEvidenceRecordData evidence = new KnowledgeEvidenceRecordData
            {
                evidenceId = evidenceId,
                observerPersonId = PersonId,
                sourceId = request.SourceId ?? string.Empty,
                informationSourceId = request.InformationSourceId ?? string.Empty,
                acquisitionSource = request.AcquisitionSource,
                provenance = request.Provenance,
                direction = request.Direction,
                proposition = request.Proposition.Clone(),
                rawStrength = KnowledgeConfidence.Clamp(request.Strength),
                effectiveStrength = KnowledgeConfidence.Clamp(request.EffectiveStrengthOverride ?? request.Strength),
                reliabilityPolicyId = request.ReliabilityPolicyId ?? string.Empty,
                reliabilityEvaluationId = request.ReliabilityEvaluationId ?? string.Empty,
                strength = KnowledgeConfidence.Clamp(request.EffectiveStrengthOverride ?? request.Strength),
                credibility = KnowledgeConfidence.Clamp(request.Credibility),
                gameTimeSeconds = Math.Max(0d, request.GameTimeSeconds),
                locationContextId = request.Proposition.locationContextId,
                bodyContextId = request.Proposition.bodyContextId,
                visibility = request.Visibility,
                relatedEventId = request.RelatedEventId ?? string.Empty,
                tags = request.Tags ?? Array.Empty<string>()
            };

            KnowledgeBeliefRecordData resultingData = priorData == null
                ? CreateBeliefData(PersonId, request.Proposition, definition, request.GameTimeSeconds, request.Visibility)
                : priorData.Clone();
            ApplyEvidenceToBelief(resultingData, evidence, definition, request.MarkAsMisconception, request.HasTruthAuthorization);
            KnowledgeBeliefRecord resultingBelief = new KnowledgeBeliefRecord(resultingData, definition, ResolvePolicy());
            KnowledgeEvidenceRecord evidenceRecord = new KnowledgeEvidenceRecord(evidence);
            KnowledgeDiscovery discovery = BuildDiscovery(priorBelief, resultingBelief, evidenceRecord);

            if (preview)
            {
                return KnowledgeOperationResult.Success("Knowledge observation preview resolved without mutation.", request.TransactionId, priorBelief, resultingBelief, evidenceRecord, discovery, priorRevision, priorRevision, preview: true);
            }

            evidenceById[evidence.evidenceId] = evidence.Clone();
            beliefsById[resultingData.beliefId] = resultingData.Clone();
            beliefIdByProposition[propositionId] = resultingData.beliefId;
            KnowledgeRevision++;
            RememberTransaction(request.TransactionId, KnowledgeResultCode.Success, resultingData.beliefId, evidence.evidenceId);
            KnowledgeOperationResult operation = KnowledgeOperationResult.Success("Knowledge observation recorded.", request.TransactionId, priorBelief, resultingBelief, evidenceRecord, discovery, priorRevision, KnowledgeRevision);
            if (!restoring)
            {
                RaiseChanged(operation);
            }

            return operation;
        }

        private void ApplyEvidenceToBelief(KnowledgeBeliefRecordData belief, KnowledgeEvidenceRecordData evidence, KnowledgeFactDefinition definition, bool markMisconception, bool authorizedTruthComparison)
        {
            List<string> supporting = new List<string>(belief.supportingEvidenceIds ?? Array.Empty<string>());
            List<string> opposing = new List<string>(belief.opposingEvidenceIds ?? Array.Empty<string>());
            List<string> sources = new List<string>(belief.sourceIds ?? Array.Empty<string>());
            int weighted = Mathf.RoundToInt(evidence.strength * (evidence.credibility / 1000f));
            if (evidence.direction == KnowledgeEvidenceDirection.Opposes)
            {
                if (!opposing.Contains(evidence.evidenceId, StringComparer.Ordinal))
                {
                    opposing.Add(evidence.evidenceId);
                }

                belief.confidence = KnowledgeConfidence.Clamp(belief.confidence - weighted);
                belief.disputed = supporting.Count > 0;
            }
            else
            {
                if (!supporting.Contains(evidence.evidenceId, StringComparer.Ordinal))
                {
                    supporting.Add(evidence.evidenceId);
                }

                int baseConfidence = belief.confidence == 0 ? weighted : belief.confidence + Mathf.Max(1, weighted / 2);
                belief.confidence = KnowledgeConfidence.Clamp(baseConfidence);
                if (evidence.direction == KnowledgeEvidenceDirection.Corrects && authorizedTruthComparison)
                {
                    belief.truthState = KnowledgeTruthState.Aligned;
                    belief.disputed = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(evidence.sourceId) && !sources.Contains(evidence.sourceId, StringComparer.Ordinal))
            {
                sources.Add(evidence.sourceId);
            }

            if (markMisconception && authorizedTruthComparison)
            {
                belief.truthState = KnowledgeTruthState.Misconception;
            }

            belief.supportingEvidenceIds = supporting.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            belief.opposingEvidenceIds = opposing.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            belief.sourceIds = sources.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            belief.lastUpdatedGameTimeSeconds = Math.Max(belief.lastUpdatedGameTimeSeconds, evidence.gameTimeSeconds);
            if (evidence.direction == KnowledgeEvidenceDirection.Corrects)
            {
                belief.lastVerifiedGameTimeSeconds = Math.Max(belief.lastVerifiedGameTimeSeconds, evidence.gameTimeSeconds);
            }

            belief.freshness = definition.StalenessPolicy == KnowledgeStalenessPolicy.HistoricalOnly
                ? KnowledgeFreshnessState.Historical
                : KnowledgeFreshnessState.Current;
            belief.beliefRevision++;
        }

        private KnowledgeBeliefRecordData CreateBeliefData(string ownerPersonId, KnowledgePropositionData proposition, KnowledgeFactDefinition definition, double time, KnowledgeVisibility visibility)
        {
            string propositionId = KnowledgeProposition.BuildIdentity(proposition);
            return new KnowledgeBeliefRecordData
            {
                beliefId = StableBeliefId(ownerPersonId, propositionId),
                personId = ownerPersonId,
                proposition = proposition.Clone(),
                confidence = 0,
                firstLearnedGameTimeSeconds = Math.Max(0d, time),
                lastUpdatedGameTimeSeconds = Math.Max(0d, time),
                lastVerifiedGameTimeSeconds = 0d,
                supportingEvidenceIds = Array.Empty<string>(),
                opposingEvidenceIds = Array.Empty<string>(),
                sourceIds = Array.Empty<string>(),
                visibility = visibility,
                freshness = definition.StalenessPolicy == KnowledgeStalenessPolicy.HistoricalOnly ? KnowledgeFreshnessState.Historical : KnowledgeFreshnessState.Current,
                truthState = KnowledgeTruthState.NotCompared,
                beliefRevision = 0
            };
        }

        private KnowledgeDiscovery BuildDiscovery(KnowledgeBeliefRecord prior, KnowledgeBeliefRecord resulting, KnowledgeEvidenceRecord evidence)
        {
            string category = prior == null ? "FirstAwareness" : resulting.State == KnowledgeBeliefState.Misconception ? "MisconceptionCreated" : resulting.State == KnowledgeBeliefState.Known && prior.State != KnowledgeBeliefState.Known ? "ConfidenceThresholdCrossed" : "BeliefUpdated";
            int delta = resulting == null ? 0 : resulting.Confidence - (prior == null ? 0 : prior.Confidence);
            return new KnowledgeDiscovery(category, prior, resulting, evidence, delta, $"{category}: {resulting?.BeliefId}");
        }

        private void RecalculateReadiness(bool restoring)
        {
            diagnostics.Clear();
            if (restoring)
            {
                Readiness = KnowledgeReadinessState.Restoring;
            }

            if (string.IsNullOrWhiteSpace(PersonId))
            {
                diagnostics.Add("Knowledge runtime is waiting for an exact Person ID.");
                Readiness = KnowledgeReadinessState.WaitingForPerson;
                return;
            }

            if (registry == null)
            {
                diagnostics.Add("Knowledge runtime is waiting for definition registry.");
                Readiness = KnowledgeReadinessState.WaitingForDefinitions;
                return;
            }

            Readiness = KnowledgeReadinessState.Ready;
        }

        private KnowledgeBeliefRecord WrapBelief(KnowledgeBeliefRecordData data)
        {
            TryResolveFact(data?.proposition?.factDefinitionId, out KnowledgeFactDefinition definition);
            return data == null ? null : new KnowledgeBeliefRecord(data, definition, ResolvePolicy());
        }

        private KnowledgePolicyDefinition ResolvePolicy()
        {
            return registry != null && registry.TryGet(KnowledgePolicyDefinition.DefaultPolicyId, out KnowledgePolicyDefinition policy)
                ? policy
                : null;
        }

        private static InformationSubjectReferenceData BuildKnowledgeSubject(KnowledgeBeliefRecord belief)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Belief,
                subjectId = belief?.BeliefId ?? string.Empty,
                parentSubjectId = KnowledgeProposition.BuildIdentity(belief?.Data.proposition),
                ownerPersonId = belief?.PersonId ?? string.Empty,
                tags = new[] { "knowledge", belief?.Proposition.FactDefinitionId ?? string.Empty }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            };
        }

        private static string[] KnowledgeDetailIds(KnowledgeBeliefRecord belief)
        {
            return new[]
            {
                "detail.summary",
                "detail.belief",
                "detail.proposition",
                "detail.fact",
                "detail.subject",
                "detail.object",
                "detail.value",
                "detail.confidence",
                "detail.evidence",
                "detail.sources",
                "detail.truth",
                "detail.context"
            };
        }

        private KnowledgeBeliefRecord MaterializeKnowledgeBelief(KnowledgeBeliefRecord belief, IReadOnlyDictionary<string, InformationRedactionState> details)
        {
            if (belief == null)
            {
                return null;
            }

            if (details.Values.All(state => state == InformationRedactionState.Visible))
            {
                return belief;
            }

            KnowledgeBeliefRecordData data = belief.Data.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.belief")) data.beliefId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.confidence")) data.confidence = 0;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.evidence")) { data.supportingEvidenceIds = Array.Empty<string>(); data.opposingEvidenceIds = Array.Empty<string>(); }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.sources")) data.sourceIds = Array.Empty<string>();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.truth")) data.truthState = KnowledgeTruthState.NotCompared;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.proposition"))
            {
                data.proposition = new KnowledgePropositionData();
            }
            else if (data.proposition != null)
            {
                KnowledgePropositionData proposition = data.proposition.Clone();
                if (!InformationAccessProjectionUtility.IsVisible(details, "detail.fact")) proposition.factDefinitionId = string.Empty;
                if (!InformationAccessProjectionUtility.IsVisible(details, "detail.subject")) { proposition.subjectId = string.Empty; proposition.subjectType = KnowledgeSubjectType.Unknown; }
                if (!InformationAccessProjectionUtility.IsVisible(details, "detail.object")) { proposition.objectId = string.Empty; proposition.objectType = KnowledgeSubjectType.Unknown; }
                if (!InformationAccessProjectionUtility.IsVisible(details, "detail.value")) { proposition.stableValueId = string.Empty; proposition.qualitativeValue = string.Empty; proposition.numericValue = 0; proposition.booleanValue = false; }
                if (!InformationAccessProjectionUtility.IsVisible(details, "detail.context")) { proposition.locationContextId = string.Empty; proposition.bodyContextId = string.Empty; proposition.sourceContextId = string.Empty; }
                data.proposition = proposition;
            }

            return WrapBelief(data);
        }

        private bool TryResolveFact(string factId, out KnowledgeFactDefinition definition)
        {
            definition = null;
            if (registry != null && registry.TryGet(factId, out definition))
            {
                return true;
            }

            return false;
        }

        private bool VisibilityAllowsAcquisition(KnowledgeVisibility visibility, bool privateAuthorized, bool truthAuthorized, out KnowledgeResultCode code, out string reason)
        {
            code = KnowledgeResultCode.Success;
            reason = string.Empty;
            if (visibility == KnowledgeVisibility.DiagnosticOnly && !truthAuthorized)
            {
                code = KnowledgeResultCode.DiagnosticFactBlocked;
                reason = "Diagnostic-only Knowledge cannot be acquired through ordinary observation.";
                return false;
            }

            if ((visibility == KnowledgeVisibility.Private || visibility == KnowledgeVisibility.Confidential || visibility == KnowledgeVisibility.Hidden || visibility == KnowledgeVisibility.Secret || visibility == KnowledgeVisibility.DevelopmentOnly) && !privateAuthorized && !truthAuthorized)
            {
                code = KnowledgeResultCode.PrivateFactBlocked;
                reason = $"Knowledge visibility '{visibility}' blocks ordinary acquisition.";
                return false;
            }

            return true;
        }

        private KnowledgeOperationResult DuplicateResult(KnowledgeProcessedTransactionData processed, string transactionId)
        {
            KnowledgeBeliefRecord belief = !string.IsNullOrWhiteSpace(processed.beliefId) && beliefsById.TryGetValue(processed.beliefId, out KnowledgeBeliefRecordData data) ? WrapBelief(data) : null;
            KnowledgeEvidenceRecord evidence = !string.IsNullOrWhiteSpace(processed.evidenceId) && evidenceById.TryGetValue(processed.evidenceId, out KnowledgeEvidenceRecordData evidenceData) ? new KnowledgeEvidenceRecord(evidenceData) : null;
            return KnowledgeOperationResult.Success("Duplicate Knowledge transaction ignored.", transactionId, belief, belief, evidence, null, KnowledgeRevision, KnowledgeRevision, duplicate: true);
        }

        private void RememberTransaction(string transactionId, KnowledgeResultCode code, string beliefId, string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedTransactions[TransactionKey(transactionId)] = new KnowledgeProcessedTransactionData
            {
                transactionId = transactionId,
                code = code,
                beliefId = beliefId ?? string.Empty,
                evidenceId = evidenceId ?? string.Empty,
                revision = KnowledgeRevision
            };
        }

        private void RaiseChanged(KnowledgeOperationResult result)
        {
            if (!suppressEvents)
            {
                KnowledgeChanged?.Invoke(this, result);
            }
        }

        private string TransactionKey(string transactionId)
        {
            return $"{PersonId}:{transactionId}";
        }

        private static string StableBeliefId(string ownerPersonId, string propositionId)
        {
            return $"belief.{StableHash(ownerPersonId + "|" + propositionId)}";
        }

        private static string StableEvidenceId(string ownerPersonId, string transactionId, string propositionId)
        {
            return $"evidence.{StableHash(ownerPersonId + "|" + transactionId + "|" + propositionId)}";
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("x16");
            }
        }
    }
}
