using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Norms
{
    public sealed class SocialNormRuntime : IDisposable
    {
        private readonly Dictionary<string, SocialNormAssessmentRecordData> assessmentsById = new Dictionary<string, SocialNormAssessmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialNormProcessedTransactionData> processedTransactions = new Dictionary<string, SocialNormProcessedTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByActor = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByTarget = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByNorm = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByObserver = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByAudience = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByInteraction = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> assessmentIdsByPromise = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private RelationshipRuntime relationships;
        private InterpersonalAttitudeRuntime attitudes;
        private ReputationRuntime reputation;
        private RumorRuntime rumors;
        private SocialInteractionRuntime interactions;
        private bool restoring;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => registry != null && !disposed;
        public int Count => assessmentsById.Count;
        public IReadOnlyList<SocialNormAssessmentSnapshot> Snapshots => Ordered(assessmentsById.Values).Select(record => new SocialNormAssessmentSnapshot(record)).ToArray();

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> knownPersons,
            RelationshipRuntime relationshipRuntime = null,
            InterpersonalAttitudeRuntime attitudeRuntime = null,
            ReputationRuntime reputationRuntime = null,
            RumorRuntime rumorRuntime = null,
            SocialInteractionRuntime interactionRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>((knownPersons ?? knownPersonIds).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            relationships = relationshipRuntime ?? relationships;
            attitudes = attitudeRuntime ?? attitudes;
            reputation = reputationRuntime ?? reputation;
            rumors = rumorRuntime ?? rumors;
            interactions = interactionRuntime ?? interactions;
            disposed = false;
            RebuildIndexes();
        }

        public SocialNormEvaluationResult Preview(SocialNormEvaluationRequest request)
        {
            SocialNormEvaluationRequest clone = request?.Clone() ?? new SocialNormEvaluationRequest();
            clone.Preview = true;
            return Execute(clone);
        }

        public SocialNormEvaluationResult Execute(SocialNormEvaluationRequest request)
        {
            request ??= new SocialNormEvaluationRequest();
            long before = Revision;
            if (!IsReady || restoring)
            {
                return SocialNormEvaluationResult.Failure(SocialNormOperationStatus.RuntimeNotReady, "Social Norm runtime is not ready.", request.TransactionId, before);
            }

            string transactionId = Clean(request.TransactionId);
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return SocialNormEvaluationResult.Failure(SocialNormOperationStatus.MissingTransactionId, "Social norm evaluation requires a transaction ID.", transactionId, before);
            }

            if (!request.Preview && processedTransactions.TryGetValue(transactionId, out SocialNormProcessedTransactionData processed))
            {
                SocialNormAssessmentSnapshot[] duplicateAssessments = processed.assessmentRecordIds
                    .Select(id => TryGetSnapshot(id, out SocialNormAssessmentSnapshot snapshot) ? snapshot : null)
                    .Where(snapshot => snapshot != null)
                    .ToArray();
                return SocialNormEvaluationResult.Success(SocialNormOperationStatus.Duplicate, "Social norm transaction was already processed.", transactionId, duplicateAssessments, duplicateAssessments, before, before, duplicate: true);
            }

            if (!ValidateRequest(request, out SocialNormOperationStatus failureStatus, out string failure))
            {
                return SocialNormEvaluationResult.Failure(failureStatus, failure, transactionId, before);
            }

            SocialNormAssessmentRecordData[] plan = BuildPlan(request, transactionId).ToArray();
            if (plan.Length == 0)
            {
                return SocialNormEvaluationResult.Success(request.Preview ? SocialNormOperationStatus.Preview : SocialNormOperationStatus.Succeeded, "No social norms were applicable.", transactionId, Array.Empty<SocialNormAssessmentSnapshot>(), Array.Empty<SocialNormAssessmentSnapshot>(), before, before, preview: request.Preview);
            }

            SocialNormAssessmentSnapshot[] candidateSnapshots = plan.Select(record => new SocialNormAssessmentSnapshot(record)).ToArray();
            if (request.Preview)
            {
                return SocialNormEvaluationResult.Success(SocialNormOperationStatus.Preview, "Social norm preview succeeded.", transactionId, candidateSnapshots, candidateSnapshots, before, before, preview: true);
            }

            foreach (SocialNormAssessmentRecordData record in plan)
            {
                if (assessmentsById.ContainsKey(record.assessmentRecordId))
                {
                    return SocialNormEvaluationResult.Failure(SocialNormOperationStatus.DuplicateAssessmentId, $"Social norm assessment '{record.assessmentRecordId}' already exists.", transactionId, before);
                }
            }

            SocialNormRuntimeSaveData rollback = CreateSaveData();
            SocialNormConsequenceRecordData[] consequences = plan.SelectMany(item => item.consequences ?? Array.Empty<SocialNormConsequenceRecordData>()).ToArray();
            InterpersonalAttitudeRuntimeSaveData attitudeRollback = consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.InterpersonalAttitude) ? attitudes?.CreateSaveData() : null;
            ReputationRuntimeSaveData reputationRollback = consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.Reputation) ? reputation?.CreateSaveData() : null;
            RelationshipRuntimeSaveData relationshipRollback = consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.Relationship) ? relationships?.CreateSaveData() : null;
            RumorRuntimeSaveData rumorRollback = consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.Rumor) ? rumors?.CreateSaveData() : null;

            if (!CommitConsequences(plan, request, out SocialNormOperationStatus consequenceStatus, out string consequenceFailure))
            {
                RestoreInternal(rollback);
                RestoreExternal(attitudeRollback, reputationRollback, relationshipRollback, rumorRollback);
                return SocialNormEvaluationResult.Failure(consequenceStatus, consequenceFailure, transactionId, before);
            }

            foreach (SocialNormAssessmentRecordData record in plan)
            {
                Revision++;
                record.revision = Revision;
                assessmentsById[record.assessmentRecordId] = record.Clone();
            }

            processedTransactions[transactionId] = new SocialNormProcessedTransactionData
            {
                transactionId = transactionId,
                assessmentRecordIds = plan.Select(record => record.assessmentRecordId).ToArray(),
                status = SocialNormOperationStatus.Succeeded,
                revision = Revision
            };

            IsDirty = true;
            RebuildIndexes();
            SocialNormAssessmentSnapshot[] committed = plan.Select(record => new SocialNormAssessmentSnapshot(record)).ToArray();
            return SocialNormEvaluationResult.Success(SocialNormOperationStatus.Succeeded, "Social norm evaluation committed.", transactionId, committed, candidateSnapshots, before, Revision);
        }

        public bool TryGetSnapshot(string assessmentRecordId, out SocialNormAssessmentSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(assessmentRecordId) && assessmentsById.TryGetValue(assessmentRecordId.Trim(), out SocialNormAssessmentRecordData record))
            {
                snapshot = new SocialNormAssessmentSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByActor(string actorPersonId) => QueryIndex(assessmentIdsByActor, actorPersonId);
        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByTarget(string targetPersonId) => QueryIndex(assessmentIdsByTarget, targetPersonId);
        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByNorm(string normDefinitionId) => QueryIndex(assessmentIdsByNorm, normDefinitionId);
        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByObserver(string observerPersonId) => QueryIndex(assessmentIdsByObserver, observerPersonId);
        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByAudience(string audienceId) => QueryIndex(assessmentIdsByAudience, audienceId);
        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByInteraction(string interactionRecordId) => QueryIndex(assessmentIdsByInteraction, interactionRecordId);
        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByPromise(string promiseId) => QueryIndex(assessmentIdsByPromise, promiseId);

        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByClassification(SocialNormAssessmentClassification classification)
        {
            return Query(record => record.classification == classification);
        }

        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryBySeverity(int minimumSeverity)
        {
            return Query(record => record.severity >= minimumSeverity);
        }

        public IReadOnlyList<SocialNormAssessmentSnapshot> QueryByTime(double minimumWorldTime, double maximumWorldTime)
        {
            return Query(record => record.occurrenceWorldTime >= minimumWorldTime && record.occurrenceWorldTime <= maximumWorldTime);
        }

        public SocialNormRuntimeSaveData CreateSaveData()
        {
            return new SocialNormRuntimeSaveData
            {
                schemaVersion = SocialNormRuntimeSaveData.CurrentSchemaVersion,
                revision = Revision,
                assessments = Ordered(assessmentsById.Values).Select(record => record.Clone()).ToList(),
                processedTransactions = processedTransactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public int PruneHistory(int maximumAssessments)
        {
            string[] remove = assessmentsById.Values
                .OrderByDescending(item => item.occurrenceWorldTime)
                .ThenByDescending(item => item.revision)
                .Skip(Math.Max(0, maximumAssessments))
                .Select(item => item.assessmentRecordId)
                .ToArray();
            foreach (string id in remove) assessmentsById.Remove(id);
            HashSet<string> retained = new HashSet<string>(assessmentsById.Keys, StringComparer.Ordinal);
            foreach (string tx in processedTransactions.Values.Where(item => !(item.assessmentRecordIds ?? Array.Empty<string>()).Any(retained.Contains)).Select(item => item.transactionId).ToArray()) processedTransactions.Remove(tx);
            if (remove.Length > 0) { Revision++; IsDirty = true; RebuildIndexes(); }
            return remove.Length;
        }

        public SocialNormEvaluationResult RestoreFromSaveData(SocialNormRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failureReason))
            {
                return SocialNormEvaluationResult.Failure(SocialNormOperationStatus.RestoreFailed, failureReason, string.Empty, before);
            }

            Configure(definitionRegistry, persons);
            restoring = true;
            RestoreInternal(saveData ?? new SocialNormRuntimeSaveData());
            restoring = false;
            IsDirty = !restoringState;
            return SocialNormEvaluationResult.Success(SocialNormOperationStatus.Succeeded, "Social norms restored.", string.Empty, Array.Empty<SocialNormAssessmentSnapshot>(), Array.Empty<SocialNormAssessmentSnapshot>(), before, Revision);
        }

        public static bool ValidateSaveData(SocialNormRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failureReason)
        {
            failureReason = string.Empty;
            SocialNormRuntimeSaveData effective = saveData ?? new SocialNormRuntimeSaveData();
            if (effective.schemaVersion != SocialNormRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Social Norm save schema version {effective.schemaVersion}.";
                return false;
            }

            if (definitionRegistry == null)
            {
                failureReason = "Social Norm restore requires a DefinitionRegistry.";
                return false;
            }

            HashSet<string> known = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialNormAssessmentRecordData record in effective.assessments ?? new List<SocialNormAssessmentRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.assessmentRecordId))
                {
                    failureReason = "Social Norm assessment save data contains a missing assessment ID.";
                    return false;
                }

                if (!ids.Add(record.assessmentRecordId.Trim()))
                {
                    failureReason = $"Duplicate Social Norm assessment ID '{record.assessmentRecordId}'.";
                    return false;
                }

                if (!definitionRegistry.TryGet(record.normDefinitionId, out SocialNormDefinition _))
                {
                    failureReason = $"Social Norm assessment '{record.assessmentRecordId}' references missing norm '{record.normDefinitionId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.actorPersonId) || !known.Contains(record.actorPersonId.Trim()))
                {
                    failureReason = $"Social Norm assessment '{record.assessmentRecordId}' references unknown actor '{record.actorPersonId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(record.targetPersonId) && !known.Contains(record.targetPersonId.Trim()))
                {
                    failureReason = $"Social Norm assessment '{record.assessmentRecordId}' references unknown target '{record.targetPersonId}'.";
                    return false;
                }

                foreach (string observer in record.witnessPersonIds ?? Array.Empty<string>())
                {
                    if (!known.Contains(observer))
                    {
                        failureReason = $"Social Norm assessment '{record.assessmentRecordId}' references unknown observer '{observer}'.";
                        return false;
                    }
                }
            }

            HashSet<string> transactions = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialNormProcessedTransactionData processed in effective.processedTransactions ?? new List<SocialNormProcessedTransactionData>())
            {
                if (processed == null || string.IsNullOrWhiteSpace(processed.transactionId) || !transactions.Add(processed.transactionId.Trim()))
                {
                    failureReason = "Social Norm save data contains duplicate or missing processed transaction IDs.";
                    return false;
                }

                foreach (string id in processed.assessmentRecordIds ?? Array.Empty<string>())
                {
                    if (!ids.Contains(id))
                    {
                        failureReason = $"Social Norm processed transaction '{processed.transactionId}' references missing assessment '{id}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        public void Clear()
        {
            assessmentsById.Clear();
            processedTransactions.Clear();
            Revision = 0L;
            IsDirty = false;
            RebuildIndexes();
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
        }

        private bool ValidateRequest(SocialNormEvaluationRequest request, out SocialNormOperationStatus status, out string failure)
        {
            if (registry == null)
            {
                status = SocialNormOperationStatus.MissingDefinitionRegistry;
                failure = "Social norm evaluation requires a DefinitionRegistry.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ActorPersonId))
            {
                status = SocialNormOperationStatus.MissingActor;
                failure = "Social norm evaluation requires an actor.";
                return false;
            }

            if (!knownPersonIds.Contains(request.ActorPersonId.Trim()))
            {
                status = SocialNormOperationStatus.UnknownActor;
                failure = $"Unknown social norm actor '{request.ActorPersonId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.TargetPersonId) && !knownPersonIds.Contains(request.TargetPersonId.Trim()))
            {
                status = SocialNormOperationStatus.UnknownTarget;
                failure = $"Unknown social norm target '{request.TargetPersonId}'.";
                return false;
            }

            foreach (string observer in request.WitnessPersonIds ?? Array.Empty<string>())
            {
                if (!knownPersonIds.Contains(observer))
                {
                    status = SocialNormOperationStatus.UnknownObserver;
                    failure = $"Unknown social norm observer '{observer}'.";
                    return false;
                }
            }

            foreach (string normId in request.RequestedNormIds ?? Array.Empty<string>())
            {
                if (!registry.TryGet(normId, out SocialNormDefinition _))
                {
                    status = SocialNormOperationStatus.MissingNormDefinition;
                    failure = $"Requested Social Norm '{normId}' is missing.";
                    return false;
                }
            }

            status = SocialNormOperationStatus.Succeeded;
            failure = string.Empty;
            return true;
        }

        private IEnumerable<SocialNormAssessmentRecordData> BuildPlan(SocialNormEvaluationRequest request, string transactionId)
        {
            SocialNormDefinition[] definitions = ResolveDefinitions(request).ToArray();
            List<SocialNormAssessmentRecordData> records = new List<SocialNormAssessmentRecordData>();
            int index = 0;
            foreach (SocialNormDefinition definition in definitions)
            {
                SocialNormAssessmentRecordData record = EvaluateDefinition(definition, request, transactionId, index++);
                if (record.applicability != SocialNormApplicabilityStatus.NotApplicable || (request.RequestedNormIds ?? Array.Empty<string>()).Contains(definition.Id))
                {
                    records.Add(record);
                }
            }

            ApplyConflicts(records);
            foreach (SocialNormAssessmentRecordData record in records)
            {
                record.consequences = BuildConsequences(record, request).ToArray();
            }

            return records.OrderBy(record => record.normDefinitionId, StringComparer.Ordinal);
        }

        private IEnumerable<SocialNormDefinition> ResolveDefinitions(SocialNormEvaluationRequest request)
        {
            string[] requested = CleanMany(request.RequestedNormIds);
            if (requested.Length > 0)
            {
                foreach (string id in requested)
                {
                    if (registry.TryGet(id, out SocialNormDefinition definition))
                    {
                        yield return definition;
                    }
                }

                yield break;
            }

            foreach (SocialNormDefinition definition in registry.DefinitionsById.Values.OfType<SocialNormDefinition>().OrderBy(definition => definition.Id, StringComparer.Ordinal))
            {
                yield return definition;
            }
        }

        private SocialNormAssessmentRecordData EvaluateDefinition(SocialNormDefinition definition, SocialNormEvaluationRequest request, string transactionId, int index)
        {
            List<SocialNormConditionEvaluationData> conditions = EvaluateConditions(definition, request).ToList();
            bool hardFailed = conditions.Any(condition => !condition.passed && !condition.optional);
            SocialNormApplicabilityStatus applicability = hardFailed ? SocialNormApplicabilityStatus.NotApplicable : SocialNormApplicabilityStatus.Applicable;
            SocialNormAssessmentClassification classification = applicability == SocialNormApplicabilityStatus.Applicable
                ? ResolveClassification(definition, request)
                : SocialNormAssessmentClassification.NotApplicable;
            int severity = applicability == SocialNormApplicabilityStatus.Applicable ? definition.BaseSeverity : 0;
            SocialNormExceptionResultData[] exceptions = ApplyExceptions(definition, request, ref applicability, ref classification, ref severity).ToArray();
            SocialNormObserverResultData[] observers = BuildObservers(request, classification, severity).ToArray();
            string assessmentId = string.IsNullOrWhiteSpace(request.AssessmentRecordId)
                ? BuildStableId("social-norm-assessment", $"{transactionId}.{definition.Id}.{index}")
                : index == 0 ? Clean(request.AssessmentRecordId) : BuildStableId("social-norm-assessment", $"{request.AssessmentRecordId}.{definition.Id}.{index}");

            return new SocialNormAssessmentRecordData
            {
                assessmentRecordId = assessmentId,
                transactionId = transactionId,
                normDefinitionId = definition.Id,
                actorPersonId = Clean(request.ActorPersonId),
                targetPersonId = Clean(request.TargetPersonId),
                interactionRecordId = Clean(request.InteractionRecordId),
                interactionDefinitionId = Clean(request.InteractionDefinitionId),
                historicalEventId = Clean(request.HistoricalEventId),
                promiseId = Clean(request.PromiseId),
                subject = request.Subject?.Clone() ?? new SocialInteractionSubjectData(),
                placeId = Clean(request.PlaceId),
                audienceId = Clean(request.AudienceId),
                witnessPersonIds = CleanMany(request.WitnessPersonIds),
                contextTags = CleanMany(request.ContextTags),
                applicability = applicability,
                classification = classification,
                actorKnowledge = request.ActorKnowledge,
                visibility = ToNormVisibility(request.Visibility),
                severity = Math.Max(0, severity),
                priority = definition.Priority,
                occurrenceWorldTime = request.OccurrenceWorldTime,
                evaluationWorldTime = request.EvaluationWorldTime,
                conditions = conditions.ToArray(),
                exceptions = exceptions,
                observers = observers,
                diagnostics = BuildDiagnostics(definition, applicability, classification, severity, conditions, exceptions),
                revision = 1L
            };
        }

        private IEnumerable<SocialNormConditionEvaluationData> EvaluateConditions(SocialNormDefinition definition, SocialNormEvaluationRequest request)
        {
            if (definition.RequiresTarget)
            {
                yield return ConditionResult("target-required", !string.IsNullOrWhiteSpace(request.TargetPersonId), false, "Target is required.");
            }

            if (definition.RequiresWitness)
            {
                yield return ConditionResult("witness-required", (request.WitnessPersonIds ?? Array.Empty<string>()).Length > 0, false, "At least one witness is required.");
            }

            if (definition.RequiresPublic)
            {
                yield return ConditionResult("public-required", request.Visibility == SocialInteractionVisibility.Public, false, "Public context is required.");
            }

            if (!string.IsNullOrWhiteSpace(definition.ExpectedInteractionDefinitionId))
            {
                bool matches = string.Equals(definition.ExpectedInteractionDefinitionId, request.InteractionDefinitionId, StringComparison.Ordinal)
                    || IsObligationStrength(definition.Strength);
                yield return ConditionResult("interaction-context", matches, false, $"Expected interaction '{definition.ExpectedInteractionDefinitionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(definition.ExpectedPromiseState))
            {
                bool matches = (request.ContextTags ?? Array.Empty<string>()).Any(tag => string.Equals(tag, $"promise-state.{definition.ExpectedPromiseState}", StringComparison.OrdinalIgnoreCase));
                if (!matches && !string.IsNullOrWhiteSpace(request.PromiseId) && interactions != null && interactions.TryGetPromise(request.PromiseId, out SocialPromiseSnapshot promise))
                {
                    matches = string.Equals(promise.Status.ToString(), definition.ExpectedPromiseState, StringComparison.OrdinalIgnoreCase);
                }

                yield return ConditionResult("promise-state", matches, false, $"Expected promise state '{definition.ExpectedPromiseState}'.");
            }

            foreach (SocialNormContextConditionData condition in definition.ApplicabilityConditions)
            {
                yield return EvaluateCondition(condition, request);
            }
        }

        private SocialNormConditionEvaluationData EvaluateCondition(SocialNormContextConditionData condition, SocialNormEvaluationRequest request)
        {
            bool passed = true;
            string reason = "Passed.";
            if (!string.IsNullOrWhiteSpace(condition.actorRoleId))
            {
                passed &= ContainsTag(request.ContextTags, $"actor-role.{condition.actorRoleId}") || ContainsTag(request.ContextTags, condition.actorRoleId);
                reason = $"Actor role '{condition.actorRoleId}'.";
            }

            if (!string.IsNullOrWhiteSpace(condition.targetRoleId))
            {
                passed &= ContainsTag(request.ContextTags, $"target-role.{condition.targetRoleId}") || ContainsTag(request.ContextTags, condition.targetRoleId);
                reason = $"Target role '{condition.targetRoleId}'.";
            }

            if (!string.IsNullOrWhiteSpace(condition.relationshipDefinitionId))
            {
                passed &= relationships != null && relationships.QueryBetween(request.ActorPersonId, request.TargetPersonId, activeOnly: true).Any(relationship => string.Equals(relationship.RelationshipDefinitionId, condition.relationshipDefinitionId, StringComparison.Ordinal));
                reason = $"Relationship '{condition.relationshipDefinitionId}'.";
            }

            if (!string.IsNullOrWhiteSpace(condition.placeId))
            {
                passed &= string.Equals(condition.placeId, request.PlaceId, StringComparison.Ordinal);
                reason = $"Place '{condition.placeId}'.";
            }

            if (!string.IsNullOrWhiteSpace(condition.audienceId))
            {
                passed &= string.Equals(condition.audienceId, request.AudienceId, StringComparison.Ordinal);
                reason = $"Audience '{condition.audienceId}'.";
            }

            if (!string.IsNullOrWhiteSpace(condition.requiredTag))
            {
                passed &= ContainsTag(request.ContextTags, condition.requiredTag);
                reason = $"Tag '{condition.requiredTag}'.";
            }

            if (condition.hasVisibility)
            {
                passed &= request.Visibility == condition.visibility;
                reason = $"Visibility '{condition.visibility}'.";
            }

            if (condition.hasChannel)
            {
                passed &= request.Channel == condition.channel;
                reason = $"Channel '{condition.channel}'.";
            }

            if (condition.requiresWitness)
            {
                passed &= (request.WitnessPersonIds ?? Array.Empty<string>()).Length > 0;
                reason = "Witness required.";
            }

            return ConditionResult(condition.conditionId, passed, condition.optional, reason);
        }

        private static SocialNormConditionEvaluationData ConditionResult(string id, bool passed, bool optional, string reason)
        {
            return new SocialNormConditionEvaluationData { conditionId = id, passed = passed, optional = optional, reason = passed ? "Passed." : reason };
        }

        private static SocialNormAssessmentClassification ResolveClassification(SocialNormDefinition definition, SocialNormEvaluationRequest request)
        {
            if (request.ConductClassification != SocialNormAssessmentClassification.Unknown)
            {
                return request.ConductClassification;
            }

            bool matchesExpected = string.IsNullOrWhiteSpace(definition.ExpectedInteractionDefinitionId)
                || string.Equals(definition.ExpectedInteractionDefinitionId, request.InteractionDefinitionId, StringComparison.Ordinal);
            return definition.Strength switch
            {
                SocialNormConductStrength.Required or SocialNormConductStrength.StronglyExpected => matchesExpected ? definition.SatisfiedClassification : definition.ViolatedClassification,
                SocialNormConductStrength.Encouraged => matchesExpected ? SocialNormAssessmentClassification.Exceeded : SocialNormAssessmentClassification.NotApplicable,
                SocialNormConductStrength.Discouraged or SocialNormConductStrength.StronglyDiscouraged or SocialNormConductStrength.Prohibited => matchesExpected ? definition.ViolatedClassification : SocialNormAssessmentClassification.NotApplicable,
                _ => definition.SatisfiedClassification
            };
        }

        private static SocialNormExceptionResultData[] ApplyExceptions(SocialNormDefinition definition, SocialNormEvaluationRequest request, ref SocialNormApplicabilityStatus applicability, ref SocialNormAssessmentClassification classification, ref int severity)
        {
            List<SocialNormExceptionResultData> results = new List<SocialNormExceptionResultData>();
            foreach (SocialNormExceptionDefinitionData exception in (definition.Exceptions ?? Array.Empty<SocialNormExceptionDefinitionData>()).Where(item => item != null).OrderBy(item => item.exceptionId, StringComparer.Ordinal))
            {
                bool applies = string.IsNullOrWhiteSpace(exception.requiredTag) || ContainsTag(request.ContextTags, exception.requiredTag);
                if (applies)
                {
                    switch (exception.effect)
                    {
                        case SocialNormExceptionEffect.MakeNotApplicable:
                            applicability = SocialNormApplicabilityStatus.NotApplicable;
                            classification = SocialNormAssessmentClassification.NotApplicable;
                            severity = 0;
                            break;
                        case SocialNormExceptionEffect.ReduceSeverity:
                            severity = Math.Max(0, severity + exception.severityDelta);
                            if (classification == SocialNormAssessmentClassification.Violation || classification == SocialNormAssessmentClassification.SeriousViolation)
                            {
                                classification = SocialNormAssessmentClassification.MinorViolation;
                            }
                            break;
                        case SocialNormExceptionEffect.ExcuseViolation:
                            severity = Math.Max(0, severity + exception.severityDelta);
                            classification = SocialNormAssessmentClassification.Excused;
                            break;
                        case SocialNormExceptionEffect.SuppressConsequences:
                            break;
                    }
                }

                results.Add(new SocialNormExceptionResultData { exceptionId = exception.exceptionId, kind = exception.kind, effect = exception.effect, applied = applies, reason = applies ? "Exception applied." : "Required exception context was not present." });
            }

            return results.ToArray();
        }

        private static IEnumerable<SocialNormObserverResultData> BuildObservers(SocialNormEvaluationRequest request, SocialNormAssessmentClassification classification, int severity)
        {
            foreach (string witness in CleanMany(request.WitnessPersonIds))
            {
                bool lacksContext = ContainsTag(request.ContextTags, $"observer-lacks-context.{witness}") || ContainsTag(request.ContextTags, "observer-lacks-context");
                yield return new SocialNormObserverResultData
                {
                    observerPersonId = witness,
                    audienceId = Clean(request.AudienceId),
                    awareness = lacksContext ? SocialNormObserverAwarenessState.Misunderstood : SocialNormObserverAwarenessState.Observed,
                    normKnowledge = lacksContext ? SocialNormActorKnowledgeState.Unavailable : SocialNormActorKnowledgeState.Knew,
                    classification = lacksContext ? SocialNormAssessmentClassification.Indeterminate : classification,
                    severity = lacksContext ? 0 : severity,
                    interpretation = lacksContext ? "Observer lacked enough context to apply the norm." : "Observer applied the norm."
                };
            }

            if (request.Visibility == SocialInteractionVisibility.Public && !string.IsNullOrWhiteSpace(request.AudienceId))
            {
                yield return new SocialNormObserverResultData
                {
                    observerPersonId = string.Empty,
                    audienceId = Clean(request.AudienceId),
                    awareness = SocialNormObserverAwarenessState.AudienceAggregate,
                    normKnowledge = SocialNormActorKnowledgeState.Knew,
                    classification = classification,
                    severity = severity,
                    interpretation = "Audience-level assessment."
                };
            }
        }

        private void ApplyConflicts(List<SocialNormAssessmentRecordData> records)
        {
            SocialNormAssessmentRecordData[] applicable = records.Where(record => record.applicability == SocialNormApplicabilityStatus.Applicable).ToArray();
            foreach (SocialNormAssessmentRecordData candidate in applicable)
            {
                if (!registry.TryGet(candidate.normDefinitionId, out SocialNormDefinition candidateDefinition))
                {
                    continue;
                }

                foreach (SocialNormAssessmentRecordData other in applicable)
                {
                    if (ReferenceEquals(candidate, other) || other.applicability != SocialNormApplicabilityStatus.Applicable)
                    {
                        continue;
                    }

                    if (candidateDefinition.OverrideNormIds.Contains(other.normDefinitionId))
                    {
                        Suppress(candidate, other, "Explicit authored override.");
                    }
                    else if (IsPotentialConflict(candidate, other) && ComparePrecedence(candidate, other) < 0)
                    {
                        Suppress(candidate, other, "Deterministic precedence.");
                    }
                }
            }
        }

        private static bool IsPotentialConflict(SocialNormAssessmentRecordData first, SocialNormAssessmentRecordData second)
        {
            return string.Equals(first.actorPersonId, second.actorPersonId, StringComparison.Ordinal)
                && string.Equals(first.targetPersonId, second.targetPersonId, StringComparison.Ordinal)
                && first.classification != second.classification
                && first.classification != SocialNormAssessmentClassification.NotApplicable
                && second.classification != SocialNormAssessmentClassification.NotApplicable;
        }

        private static int ComparePrecedence(SocialNormAssessmentRecordData first, SocialNormAssessmentRecordData second)
        {
            int priority = second.priority.CompareTo(first.priority);
            if (priority != 0)
            {
                return priority;
            }

            int severity = second.severity.CompareTo(first.severity);
            if (severity != 0)
            {
                return severity;
            }

            return string.CompareOrdinal(first.normDefinitionId, second.normDefinitionId);
        }

        private static void Suppress(SocialNormAssessmentRecordData winner, SocialNormAssessmentRecordData suppressed, string reason)
        {
            if (suppressed.applicability == SocialNormApplicabilityStatus.SuppressedByConflict)
            {
                return;
            }

            suppressed.applicability = SocialNormApplicabilityStatus.SuppressedByConflict;
            suppressed.conflicts = new[]
            {
                new SocialNormConflictResultData
                {
                    winnerNormId = winner.normDefinitionId,
                    suppressedNormId = suppressed.normDefinitionId,
                    reason = reason,
                    order = 0
                }
            };
            winner.conflicts = (winner.conflicts ?? Array.Empty<SocialNormConflictResultData>()).Concat(new[]
            {
                new SocialNormConflictResultData
                {
                    winnerNormId = winner.normDefinitionId,
                    suppressedNormId = suppressed.normDefinitionId,
                    reason = reason,
                    order = winner.conflicts?.Length ?? 0
                }
            }).ToArray();
        }

        private IEnumerable<SocialNormConsequenceRecordData> BuildConsequences(SocialNormAssessmentRecordData record, SocialNormEvaluationRequest request)
        {
            if (record.applicability != SocialNormApplicabilityStatus.Applicable || record.classification == SocialNormAssessmentClassification.NotApplicable || record.exceptions.Any(item => item.applied && item.effect == SocialNormExceptionEffect.SuppressConsequences))
            {
                yield break;
            }

            if (!registry.TryGet(record.normDefinitionId, out SocialNormDefinition definition))
            {
                yield break;
            }

            foreach (SocialNormConsequenceDefinitionData consequence in definition.Consequences.OrderBy(item => item.consequenceId, StringComparer.Ordinal))
            {
                if (consequence.appliesToClassifications?.Length > 0 && !consequence.appliesToClassifications.Contains(record.classification))
                {
                    continue;
                }

                if (consequence.publicOnly && request.Visibility != SocialInteractionVisibility.Public)
                {
                    continue;
                }

                foreach (string observer in ResolveConsequenceObservers(consequence, record))
                {
                    yield return new SocialNormConsequenceRecordData
                    {
                        consequenceId = consequence.consequenceId,
                        targetRuntime = consequence.targetRuntime,
                        operation = consequence.operation,
                        policy = consequence.policy,
                        sourceAssessmentId = record.assessmentRecordId,
                        transactionId = $"{record.transactionId}.{record.normDefinitionId}.{consequence.consequenceId}.{observer}",
                        observerPersonId = observer,
                        subjectPersonId = ResolveSubjectPerson(record, consequence, observer),
                        dimensionId = consequence.dimensionId ?? string.Empty,
                        audienceId = string.IsNullOrWhiteSpace(consequence.audienceId) ? record.audienceId : consequence.audienceId,
                        amount = consequence.amount,
                        committed = false,
                        status = "Planned",
                        message = "Consequence planned."
                    };
                }
            }
        }

        private static IEnumerable<string> ResolveConsequenceObservers(SocialNormConsequenceDefinitionData consequence, SocialNormAssessmentRecordData record)
        {
            if (consequence.observersOnly)
            {
                foreach (string observer in record.observers.Select(observer => observer.observerPersonId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                {
                    yield return observer;
                }
            }
            else
            {
                yield return record.targetPersonId;
            }
        }

        private static string ResolveSubjectPerson(SocialNormAssessmentRecordData record, SocialNormConsequenceDefinitionData consequence, string observer)
        {
            return consequence.targetRuntime == SocialNormConsequenceTargetRuntime.Reputation
                ? record.actorPersonId
                : record.actorPersonId;
        }

        private bool CommitConsequences(IEnumerable<SocialNormAssessmentRecordData> records, SocialNormEvaluationRequest request, out SocialNormOperationStatus status, out string failure)
        {
            foreach (SocialNormAssessmentRecordData record in records)
            {
                SocialNormConsequenceRecordData[] consequences = record.consequences ?? Array.Empty<SocialNormConsequenceRecordData>();
                for (int i = 0; i < consequences.Length; i++)
                {
                    SocialNormConsequenceRecordData consequence = consequences[i];
                    bool success = consequence.targetRuntime switch
                    {
                        SocialNormConsequenceTargetRuntime.InterpersonalAttitude => ApplyAttitude(consequence, record, out failure),
                        SocialNormConsequenceTargetRuntime.Reputation => ApplyReputation(consequence, record, out failure),
                        SocialNormConsequenceTargetRuntime.MemoryReference or SocialNormConsequenceTargetRuntime.HistoryReference or SocialNormConsequenceTargetRuntime.SocialInteraction or SocialNormConsequenceTargetRuntime.Promise => ApplyReference(consequence, out failure),
                        _ => ApplyReference(consequence, out failure)
                    };

                    if (!success && consequence.policy == SocialNormConsequencePolicy.Required)
                    {
                        consequence.status = "Failed";
                        consequence.message = failure;
                        status = SocialNormOperationStatus.ConsequenceFailed;
                        return false;
                    }

                    consequence.committed = success;
                    consequence.status = success ? "Succeeded" : "Skipped";
                    consequence.message = success ? "Consequence committed." : failure;
                    consequences[i] = consequence;
                }

                record.consequences = consequences;
            }

            status = SocialNormOperationStatus.Succeeded;
            failure = string.Empty;
            return true;
        }

        private bool ApplyAttitude(SocialNormConsequenceRecordData consequence, SocialNormAssessmentRecordData record, out string failure)
        {
            if (attitudes == null || string.IsNullOrWhiteSpace(consequence.observerPersonId) || string.IsNullOrWhiteSpace(consequence.subjectPersonId))
            {
                failure = "Interpersonal Attitude runtime or participants are missing.";
                return false;
            }

            AttitudeMutationResult result = attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = consequence.transactionId,
                observerPersonId = consequence.observerPersonId,
                subjectPersonId = consequence.subjectPersonId,
                dimensionId = consequence.dimensionId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                delta = consequence.amount,
                sourceId = consequence.sourceAssessmentId,
                sourceCategory = AttitudeContributionSourceCategory.Scripted,
                historicalEventId = record.historicalEventId,
                worldTime = record.evaluationWorldTime
            });
            consequence.affectedRecordId = result.RecordId;
            failure = result.Message;
            return result.Succeeded || result.Duplicate;
        }

        private bool ApplyReputation(SocialNormConsequenceRecordData consequence, SocialNormAssessmentRecordData record, out string failure)
        {
            if (reputation == null || string.IsNullOrWhiteSpace(record.actorPersonId) || string.IsNullOrWhiteSpace(consequence.audienceId))
            {
                failure = "Reputation runtime, subject, or audience is missing.";
                return false;
            }

            ReputationMutationResult result = reputation.Mutate(new ReputationMutationRequest
            {
                transactionId = consequence.transactionId,
                subjectPersonId = record.actorPersonId,
                audienceId = consequence.audienceId,
                dimensionId = consequence.dimensionId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                delta = consequence.amount,
                sourceId = consequence.sourceAssessmentId,
                sourceCategory = record.visibility == SocialNormVisibility.Public ? ReputationContributionSourceCategory.PublicSpeech : ReputationContributionSourceCategory.WitnessedDeed,
                authenticity = ReputationAuthenticity.Verified,
                historicalEventId = record.historicalEventId,
                supportingReferenceId = record.assessmentRecordId,
                worldTime = record.evaluationWorldTime
            });
            consequence.affectedRecordId = result.RecordId;
            failure = result.Message;
            return result.Succeeded || result.Duplicate;
        }

        private static bool ApplyReference(SocialNormConsequenceRecordData consequence, out string failure)
        {
            failure = "Reference consequence recorded.";
            consequence.affectedRecordId = consequence.sourceAssessmentId;
            return true;
        }

        private void RestoreExternal(InterpersonalAttitudeRuntimeSaveData attitudeRollback, ReputationRuntimeSaveData reputationRollback, RelationshipRuntimeSaveData relationshipRollback, RumorRuntimeSaveData rumorRollback)
        {
            if (attitudeRollback != null)
            {
                attitudes?.RestoreFromSaveData(attitudeRollback, registry, knownPersonIds, restoringState: true);
            }

            if (reputationRollback != null)
            {
                reputation?.RestoreFromSaveData(reputationRollback, registry, knownPersonIds, restoringState: true);
            }

            if (relationshipRollback != null)
            {
                relationships?.RestoreFromSaveData(relationshipRollback, registry, knownPersonIds, restoring: true);
            }

            if (rumorRollback != null)
            {
                rumors?.RestoreFromSaveData(rumorRollback, registry, knownPersonIds, restoringState: true);
            }
        }

        private void RestoreInternal(SocialNormRuntimeSaveData saveData)
        {
            assessmentsById.Clear();
            processedTransactions.Clear();
            Revision = saveData?.revision ?? 0L;
            foreach (SocialNormAssessmentRecordData record in saveData?.assessments ?? new List<SocialNormAssessmentRecordData>())
            {
                SocialNormAssessmentRecordData clone = record.Clone();
                assessmentsById[clone.assessmentRecordId] = clone;
            }

            foreach (SocialNormProcessedTransactionData processed in saveData?.processedTransactions ?? new List<SocialNormProcessedTransactionData>())
            {
                SocialNormProcessedTransactionData clone = processed.Clone();
                processedTransactions[clone.transactionId] = clone;
            }

            RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            assessmentIdsByActor.Clear();
            assessmentIdsByTarget.Clear();
            assessmentIdsByNorm.Clear();
            assessmentIdsByObserver.Clear();
            assessmentIdsByAudience.Clear();
            assessmentIdsByInteraction.Clear();
            assessmentIdsByPromise.Clear();
            foreach (SocialNormAssessmentRecordData record in assessmentsById.Values)
            {
                AddIndex(assessmentIdsByActor, record.actorPersonId, record.assessmentRecordId);
                AddIndex(assessmentIdsByTarget, record.targetPersonId, record.assessmentRecordId);
                AddIndex(assessmentIdsByNorm, record.normDefinitionId, record.assessmentRecordId);
                AddIndex(assessmentIdsByAudience, record.audienceId, record.assessmentRecordId);
                AddIndex(assessmentIdsByInteraction, record.interactionRecordId, record.assessmentRecordId);
                AddIndex(assessmentIdsByPromise, record.promiseId, record.assessmentRecordId);
                foreach (SocialNormObserverResultData observer in record.observers ?? Array.Empty<SocialNormObserverResultData>())
                {
                    AddIndex(assessmentIdsByObserver, observer.observerPersonId, record.assessmentRecordId);
                }
            }
        }

        private static void AddIndex(Dictionary<string, List<string>> index, string key, string id)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            key = key.Trim();
            if (!index.TryGetValue(key, out List<string> ids))
            {
                ids = new List<string>();
                index[key] = ids;
            }

            if (!ids.Contains(id))
            {
                ids.Add(id);
                ids.Sort(StringComparer.Ordinal);
            }
        }

        private IReadOnlyList<SocialNormAssessmentSnapshot> QueryIndex(Dictionary<string, List<string>> index, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !index.TryGetValue(key.Trim(), out List<string> ids))
            {
                return Array.Empty<SocialNormAssessmentSnapshot>();
            }

            return ids.Select(id => TryGetSnapshot(id, out SocialNormAssessmentSnapshot snapshot) ? snapshot : null).Where(snapshot => snapshot != null).ToArray();
        }

        private IReadOnlyList<SocialNormAssessmentSnapshot> Query(Func<SocialNormAssessmentRecordData, bool> predicate)
        {
            return Ordered(assessmentsById.Values).Where(predicate).Select(record => new SocialNormAssessmentSnapshot(record)).ToArray();
        }

        private static IEnumerable<SocialNormAssessmentRecordData> Ordered(IEnumerable<SocialNormAssessmentRecordData> records)
        {
            return (records ?? Array.Empty<SocialNormAssessmentRecordData>())
                .OrderBy(record => record.evaluationWorldTime)
                .ThenBy(record => record.assessmentRecordId, StringComparer.Ordinal);
        }

        private static SocialNormVisibility ToNormVisibility(SocialInteractionVisibility visibility)
        {
            return visibility switch
            {
                SocialInteractionVisibility.Public => SocialNormVisibility.Public,
                SocialInteractionVisibility.Witnessed => SocialNormVisibility.Witnessed,
                _ => SocialNormVisibility.Private
            };
        }

        private static bool IsObligationStrength(SocialNormConductStrength strength)
        {
            return strength == SocialNormConductStrength.Required || strength == SocialNormConductStrength.StronglyExpected;
        }

        private static string[] BuildDiagnostics(SocialNormDefinition definition, SocialNormApplicabilityStatus applicability, SocialNormAssessmentClassification classification, int severity, IEnumerable<SocialNormConditionEvaluationData> conditions, IEnumerable<SocialNormExceptionResultData> exceptions)
        {
            return new[]
            {
                $"Norm={definition.Id}",
                $"Applicability={applicability}",
                $"Classification={classification}",
                $"Severity={severity}",
                $"Conditions={string.Join(",", conditions.Select(item => $"{item.conditionId}:{item.passed}"))}",
                $"Exceptions={string.Join(",", exceptions.Select(item => $"{item.exceptionId}:{item.applied}"))}"
            };
        }

        private static string BuildStableId(string prefix, string source)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
            string suffix = BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 24).ToLowerInvariant();
            return $"{prefix}.{suffix}";
        }

        private static bool ContainsTag(IEnumerable<string> tags, string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && (tags ?? Array.Empty<string>()).Any(value => string.Equals(value, tag, StringComparison.Ordinal));
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string[] CleanMany(IEnumerable<string> values)
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
