using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    public sealed class ProfessionalActivityRuntime
    {
        private readonly Dictionary<string, ProfessionalActivityRecordData> activitiesById = new Dictionary<string, ProfessionalActivityRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionalExperienceEvidenceData> evidenceById = new Dictionary<string, ProfessionalExperienceEvidenceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionalActivitySourceSnapshot> sourceSnapshotsBySignature = new Dictionary<string, ProfessionalActivitySourceSnapshot>(StringComparer.Ordinal);
        private readonly List<ProfessionalActivityHistoryHookData> historyHooks = new List<ProfessionalActivityHistoryHookData>();
        private DefinitionRegistry definitionRegistry;
        private PersonProfessionRuntime professionRuntime;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int ActivityCount => activitiesById.Count;
        public int EvidenceCount => evidenceById.Count;
        public IReadOnlyList<ProfessionalActivityHistoryHookData> HistoryHooks => historyHooks.Select(hook => hook.Clone()).ToArray();
        public IReadOnlyList<ProfessionalActivityRecordData> Activities => activitiesById.Values.OrderBy(item => item.activityId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProfessionalExperienceEvidenceData> Evidence => evidenceById.Values.OrderBy(item => item.evidenceId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry registry, PersonProfessionRuntime professions, IEnumerable<string> persons)
        {
            definitionRegistry = registry;
            professionRuntime = professions;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public void RegisterSourceSnapshot(ProfessionalActivitySourceSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Reference == null || string.IsNullOrWhiteSpace(snapshot.Reference.sourceId))
            {
                return;
            }

            sourceSnapshotsBySignature[snapshot.Reference.Signature] = snapshot;
        }

        public bool TryGetActivity(string activityId, out ProfessionalActivityRecordData activity)
        {
            if (!string.IsNullOrWhiteSpace(activityId) && activitiesById.TryGetValue(activityId, out ProfessionalActivityRecordData found))
            {
                activity = found.Clone();
                return true;
            }

            activity = null;
            return false;
        }

        public bool TryGetEvidence(string evidenceId, out ProfessionalExperienceEvidenceData evidence)
        {
            if (!string.IsNullOrWhiteSpace(evidenceId) && evidenceById.TryGetValue(evidenceId, out ProfessionalExperienceEvidenceData found))
            {
                evidence = found.Clone();
                return true;
            }

            evidence = null;
            return false;
        }

        public ProfessionalActivityValidationResult EvaluateActivity(ProfessionalActivityRegistrationRequest request)
        {
            List<string> diagnostics = new List<string>();
            if (!TryBuildActivity(request, out ProfessionalActivityRecordData proposed, out ProfessionalActivityDefinition definition, out ProfessionalActivitySourceSnapshot source, diagnostics))
            {
                return new ProfessionalActivityValidationResult(false, MapDiagnosticStatus(diagnostics), diagnostics, proposed, source, revision);
            }

            ValidateProposedActivity(proposed, definition, source, diagnostics);
            bool valid = diagnostics.Count == 0;
            return new ProfessionalActivityValidationResult(valid, valid ? ProfessionalActivityOperationStatus.Succeeded : MapDiagnosticStatus(diagnostics), diagnostics, proposed, source, revision);
        }

        public ProfessionalActivityOperationResult ProposeActivity(ProfessionalActivityRegistrationRequest request, string transactionId, bool preview = false)
        {
            long prior = revision;
            ProfessionalActivityValidationResult evaluation = EvaluateActivity(request);
            if (!evaluation.Valid)
            {
                return ProfessionalActivityOperationResult.Failure(evaluation.Status, string.Join(" | ", evaluation.Diagnostics), revision);
            }

            ProfessionalActivityRecordData record = evaluation.ProposedActivity.Clone();
            record.state = ProfessionalActivityState.Recorded;
            record.revision = Math.Max(1L, revision + 1L);
            if (preview)
            {
                return ProfessionalActivityOperationResult.Success("Professional activity preview succeeded.", prior, prior, record, preview: true);
            }

            if (activitiesById.ContainsKey(record.activityId))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.Duplicate, $"Professional activity '{record.activityId}' already exists.", revision);
            }

            activitiesById[record.activityId] = record.Clone();
            revision++;
            AddHook(ProfessionalActivityHistoryHookKind.FirstProfessionalActivity, record, string.Empty, transactionId);
            return ProfessionalActivityOperationResult.Success("Professional activity recorded.", prior, revision, record);
        }

        public ProfessionalActivityOperationResult ValidateActivity(string activityId, string evidenceId, string validationAuthorityOrPolicyId, string transactionId, string validationWorldTime = "", bool preview = false)
        {
            long prior = revision;
            if (!activitiesById.TryGetValue(activityId ?? string.Empty, out ProfessionalActivityRecordData record))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.MissingActivity, "Professional activity is missing.", revision);
            }

            if (record.state == ProfessionalActivityState.Rejected || record.state == ProfessionalActivityState.Revoked || record.state == ProfessionalActivityState.Invalid)
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.InvalidState, $"Activity state '{record.state}' cannot create experience evidence.", revision);
            }

            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.InvalidRequest, "Experience evidence ID is required.", revision);
            }

            if (evidenceById.ContainsKey(evidenceId))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.Duplicate, $"Experience evidence '{evidenceId}' already exists.", revision);
            }

            bool exclusiveCredit = definitionRegistry != null
                && definitionRegistry.TryGet(record.activityDefinitionId, out ProfessionalActivityDefinition definition)
                && definition.CreditPolicy == ProfessionalCreditPolicy.Exclusive;
            if (exclusiveCredit && HasExclusiveDuplicate(record, ignoreActivityId: record.activityId))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.DuplicateExclusiveSource, "The same exclusive source has already produced validated professional evidence for this Person and profession.", revision);
            }

            ProfessionalExperienceEvidenceData evidence = BuildEvidence(record, evidenceId, validationAuthorityOrPolicyId, validationWorldTime);
            if (preview)
            {
                return ProfessionalActivityOperationResult.Success("Professional experience validation preview succeeded.", prior, prior, record, evidence, preview: true);
            }

            record.state = ProfessionalActivityState.Validated;
            record.revision = Math.Max(record.revision + 1L, revision + 1L);
            evidence.revision = Math.Max(1L, revision + 1L);
            activitiesById[record.activityId] = record.Clone();
            evidenceById[evidence.evidenceId] = evidence.Clone();
            revision++;
            AddHook(HookForEvidence(evidence), record, evidence.evidenceId, transactionId);
            return ProfessionalActivityOperationResult.Success("Professional experience evidence created.", prior, revision, record, evidence);
        }

        public ProfessionalActivityOperationResult RegisterAndValidateActivity(ProfessionalActivityRegistrationRequest request, string evidenceId, string validationAuthorityOrPolicyId, string transactionId, bool preview = false)
        {
            ProfessionalActivityOperationResult proposed = ProposeActivity(request, transactionId, preview);
            if (!proposed.Succeeded || preview)
            {
                return proposed;
            }

            return ValidateActivity(proposed.Activity.activityId, evidenceId, validationAuthorityOrPolicyId, transactionId, proposed.Activity.completionWorldTime);
        }

        public ProfessionalActivityOperationResult RejectActivity(string activityId, string transactionId, string reason = "")
        {
            return SetActivityState(activityId, ProfessionalActivityState.Rejected, transactionId, reason);
        }

        public ProfessionalActivityOperationResult DisputeActivity(string activityId, string transactionId, string reason = "")
        {
            return SetActivityState(activityId, ProfessionalActivityState.Disputed, transactionId, reason, ProfessionalActivityHistoryHookKind.ExperienceRecordDisputed);
        }

        public ProfessionalActivityOperationResult CorrectActivity(string activityId, ProfessionalActivityCategory category, ProfessionalExperienceCategory experienceCategory, string transactionId)
        {
            long prior = revision;
            if (!activitiesById.TryGetValue(activityId ?? string.Empty, out ProfessionalActivityRecordData activity))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.MissingActivity, "Professional activity is missing.", revision);
            }

            activity.category = category;
            activity.state = ProfessionalActivityState.Corrected;
            activity.revision++;
            foreach (ProfessionalExperienceEvidenceData evidence in evidenceById.Values.Where(item => string.Equals(item.activityId, activity.activityId, StringComparison.Ordinal)).ToArray())
            {
                evidence.category = experienceCategory;
                evidence.revision++;
                evidenceById[evidence.evidenceId] = evidence.Clone();
            }

            activitiesById[activity.activityId] = activity.Clone();
            revision++;
            AddHook(ProfessionalActivityHistoryHookKind.ExperienceRecordCorrected, activity, string.Empty, transactionId);
            return ProfessionalActivityOperationResult.Success("Professional activity classification corrected.", prior, revision, activity);
        }

        public ProfessionalActivityOperationResult RevokeActivity(string activityId, string transactionId, string reason = "")
        {
            long prior = revision;
            if (!activitiesById.TryGetValue(activityId ?? string.Empty, out ProfessionalActivityRecordData activity))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.MissingActivity, "Professional activity is missing.", revision);
            }

            activity.state = ProfessionalActivityState.Revoked;
            activity.outcomeSummary = string.IsNullOrWhiteSpace(reason) ? activity.outcomeSummary : reason;
            activity.revision++;
            foreach (ProfessionalExperienceEvidenceData evidence in evidenceById.Values.Where(item => string.Equals(item.activityId, activity.activityId, StringComparison.Ordinal)).ToArray())
            {
                evidence.outcome = ProfessionalActivityOutcomeState.Revoked;
                evidence.revision++;
                evidenceById[evidence.evidenceId] = evidence.Clone();
            }

            activitiesById[activity.activityId] = activity.Clone();
            revision++;
            AddHook(ProfessionalActivityHistoryHookKind.ExperienceRevoked, activity, string.Empty, transactionId);
            return ProfessionalActivityOperationResult.Success("Professional activity revoked.", prior, revision, activity);
        }

        public ProfessionalExperienceSummary BuildExperienceSummary(string personId, string professionId, bool includeRedacted = false)
        {
            IReadOnlyList<ProfessionalExperienceEvidenceData> evidence = evidenceById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(item.professionId, professionId ?? string.Empty, StringComparison.Ordinal)
                    && item.outcome != ProfessionalActivityOutcomeState.Revoked
                    && activitiesById.TryGetValue(item.activityId, out ProfessionalActivityRecordData activity)
                    && activity.state == ProfessionalActivityState.Validated)
                .Select(item => item.Clone())
                .ToArray();
            return new ProfessionalExperienceSummary(personId, professionId, evidence, revision, "Experience summary derived from validated professional evidence.", includeRedacted);
        }

        public bool EvaluateExperienceRequirement(string personId, ProfessionalExperienceRequirementData requirement, out ProfessionalExperienceSummary summary)
        {
            summary = BuildExperienceSummary(personId, requirement?.professionId);
            if (requirement == null)
            {
                return false;
            }

            IEnumerable<ProfessionalExperienceEvidenceData> evidence = summary.Evidence;
            if (!string.IsNullOrWhiteSpace(requirement.specializationId))
            {
                evidence = evidence.Where(item => string.Equals(item.specializationId, requirement.specializationId, StringComparison.Ordinal));
            }

            if (requirement.requiredCategory != ProfessionalExperienceCategory.Custom)
            {
                evidence = evidence.Where(item => item.category == requirement.requiredCategory);
            }

            ProfessionalExperienceEvidenceData[] filtered = evidence.ToArray();
            return filtered.Length >= requirement.minimumValidatedActivities
                && filtered.Count(item => item.responsibility == ProfessionalResponsibilityLevel.IndependentPractitioner || item.responsibility == ProfessionalResponsibilityLevel.IndependentWithReview) >= requirement.minimumIndependentActivities
                && filtered.Count(item => item.responsibility == ProfessionalResponsibilityLevel.SupervisedWorker || item.supervisionLevel == TrainingSupervisionLevel.CloselySupervised || item.supervisionLevel == TrainingSupervisionLevel.PeriodicallySupervised) >= requirement.minimumSupervisedActivities
                && filtered.Any(item => requirement.minimumDifficulty == ProfessionalActivityDifficulty.Unknown || item.difficulty >= requirement.minimumDifficulty)
                && filtered.Any(item => item.quality >= requirement.minimumQuality)
                && (!requirement.requireRecentActivity || !string.IsNullOrWhiteSpace(summary.MostRecentActivityWorldTime));
        }

        public ProfessionalActivityProjection<ProfessionalActivityRecordData> ProjectActivity(string activityId, ProfessionalActivityProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!activitiesById.TryGetValue(activityId ?? string.Empty, out ProfessionalActivityRecordData record))
            {
                return new ProfessionalActivityProjection<ProfessionalActivityRecordData>(null, audience, decision, false, true, Array.Empty<string>(), ProfessionalActivityInformationSubject.ProtectedFields);
            }

            if (decision != null && decision.Denied)
            {
                return new ProfessionalActivityProjection<ProfessionalActivityRecordData>(null, audience, decision, false, true, Array.Empty<string>(), ProfessionalActivityInformationSubject.ProtectedFields);
            }

            bool redacted = decision != null && decision.Decision == InformationAccessDecisionKind.RedactedAccess;
            ProfessionalActivityRecordData output = record.Clone();
            if (redacted)
            {
                output.personId = string.Empty;
                output.source.sourceId = string.Empty;
                output.relatedTargetIds = Array.Empty<string>();
                output.evidenceReferenceIds = Array.Empty<string>();
                output.provenance = string.Empty;
            }

            return new ProfessionalActivityProjection<ProfessionalActivityRecordData>(output, audience, decision, redacted, false, decision?.AllowedDetails ?? Array.Empty<string>(), redacted ? ProfessionalActivityInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public ProfessionalActivityRuntimeSaveData CreateSaveData()
        {
            return new ProfessionalActivityRuntimeSaveData
            {
                revision = revision,
                activities = activitiesById.Values.OrderBy(item => item.activityId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                evidence = evidenceById.Values.OrderBy(item => item.evidenceId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public ProfessionalActivityOperationResult RestoreFromSaveData(ProfessionalActivityRuntimeSaveData saveData, DefinitionRegistry registry, PersonProfessionRuntime professions, IEnumerable<string> persons, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, registry, professions, persons, out string failure))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.RestoreFailed, failure, revision);
            }

            ProfessionalActivityRuntimeSaveData rollback = CreateSaveData();
            long prior = revision;
            try
            {
                Configure(registry, professions, persons);
                activitiesById.Clear();
                evidenceById.Clear();
                foreach (ProfessionalActivityRecordData activity in saveData.activities ?? new List<ProfessionalActivityRecordData>())
                {
                    activitiesById[activity.activityId] = activity.Clone();
                }

                foreach (ProfessionalExperienceEvidenceData evidence in saveData.evidence ?? new List<ProfessionalExperienceEvidenceData>())
                {
                    evidenceById[evidence.evidenceId] = evidence.Clone();
                }

                revision = Math.Max(0L, saveData.revision);
                if (restoring)
                {
                    historyHooks.Clear();
                }

                return ProfessionalActivityOperationResult.Success("Professional activity restored.", prior, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, professions, persons, restoring);
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.RestoreFailed, $"Professional activity restore failed: {exception.Message}", prior);
            }
        }

        public static bool ValidateSaveData(ProfessionalActivityRuntimeSaveData saveData, DefinitionRegistry registry, PersonProfessionRuntime professions, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Professional activity save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ProfessionalActivityRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported professional activity schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> knownPersons = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> activityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfessionalActivityRecordData activity in saveData.activities ?? new List<ProfessionalActivityRecordData>())
            {
                if (activity == null || string.IsNullOrWhiteSpace(activity.activityId) || !activityIds.Add(activity.activityId))
                {
                    failure = "Professional activity save data contains a missing or duplicate activity ID.";
                    return false;
                }

                if (!knownPersons.Contains(activity.personId ?? string.Empty))
                {
                    failure = $"Professional activity '{activity.activityId}' references unknown Person '{activity.personId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(activity.professionId, out ProfessionDefinition _))
                {
                    failure = $"Professional activity '{activity.activityId}' references missing Profession '{activity.professionId}'.";
                    return false;
                }

                if (!HasProfessionRelationship(professions, activity.personId, activity.professionId, activeOnly: false))
                {
                    failure = $"Professional activity '{activity.activityId}' references a Person and Profession relationship that does not exist.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(activity.specializationId) && (registry == null || !registry.TryGet(activity.specializationId, out ProfessionSpecializationDefinition _)))
                {
                    failure = $"Professional activity '{activity.activityId}' references missing Specialization '{activity.specializationId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(activity.activityDefinitionId, out ProfessionalActivityDefinition _))
                {
                    failure = $"Professional activity '{activity.activityId}' references missing Activity Definition '{activity.activityDefinitionId}'.";
                    return false;
                }

                if (activity.source == null || string.IsNullOrWhiteSpace(activity.source.sourceId))
                {
                    failure = $"Professional activity '{activity.activityId}' has no source reference.";
                    return false;
                }
            }

            HashSet<string> evidenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfessionalExperienceEvidenceData evidence in saveData.evidence ?? new List<ProfessionalExperienceEvidenceData>())
            {
                if (evidence == null || string.IsNullOrWhiteSpace(evidence.evidenceId) || !evidenceIds.Add(evidence.evidenceId))
                {
                    failure = "Professional activity save data contains a missing or duplicate evidence ID.";
                    return false;
                }

                if (!activityIds.Contains(evidence.activityId ?? string.Empty))
                {
                    failure = $"Experience evidence '{evidence.evidenceId}' references missing activity '{evidence.activityId}'.";
                    return false;
                }

                if (evidence.quantityOrDuration < 0f || evidence.quality < 0)
                {
                    failure = $"Experience evidence '{evidence.evidenceId}' has invalid quantity or quality.";
                    return false;
                }
            }

            return true;
        }

        private bool TryBuildActivity(ProfessionalActivityRegistrationRequest request, out ProfessionalActivityRecordData activity, out ProfessionalActivityDefinition definition, out ProfessionalActivitySourceSnapshot source, List<string> diagnostics)
        {
            activity = null;
            definition = null;
            source = request?.Source;
            if (request == null)
            {
                diagnostics.Add("InvalidRequest");
                return false;
            }

            if (definitionRegistry == null)
            {
                diagnostics.Add("MissingRuntime");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ActivityId) || string.IsNullOrWhiteSpace(request.PersonId) || string.IsNullOrWhiteSpace(request.ProfessionId) || string.IsNullOrWhiteSpace(request.ActivityDefinitionId))
            {
                diagnostics.Add("InvalidRequest");
                return false;
            }

            if (!knownPersonIds.Contains(request.PersonId))
            {
                diagnostics.Add("MissingPerson");
                return false;
            }

            if (!definitionRegistry.TryGet(request.ProfessionId, out ProfessionDefinition _))
            {
                diagnostics.Add("MissingProfession");
                return false;
            }

            if (!HasProfessionRelationship(professionRuntime, request.PersonId, request.ProfessionId, activeOnly: true))
            {
                diagnostics.Add("MissingProfessionRelationship");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.SpecializationId) && !definitionRegistry.TryGet(request.SpecializationId, out ProfessionSpecializationDefinition _))
            {
                diagnostics.Add("SpecializationMismatch");
                return false;
            }

            if (!definitionRegistry.TryGet(request.ActivityDefinitionId, out definition))
            {
                diagnostics.Add("MissingDefinition");
                return false;
            }

            if (source == null && request.SourceReference != null)
            {
                sourceSnapshotsBySignature.TryGetValue(request.SourceReference.Signature, out source);
            }

            if (source == null)
            {
                diagnostics.Add("MissingSource");
                return false;
            }

            activity = new ProfessionalActivityRecordData
            {
                activityId = request.ActivityId,
                personId = request.PersonId,
                professionId = request.ProfessionId,
                specializationId = request.SpecializationId ?? string.Empty,
                activityDefinitionId = request.ActivityDefinitionId,
                source = source.Reference.Clone(),
                category = request.Category == ProfessionalActivityCategory.Custom ? definition.Category : request.Category,
                startWorldTime = request.StartWorldTime ?? source.WorldTime,
                completionWorldTime = request.CompletionWorldTime ?? source.WorldTime,
                state = ProfessionalActivityState.Proposed,
                outcome = request.Outcome == ProfessionalActivityOutcomeState.Unknown ? source.Outcome : request.Outcome,
                supervisionLevel = request.SupervisionLevel,
                supervisorOrInstructorIds = ProfessionalActivitySourceSnapshot.Clean(request.SupervisorOrInstructorIds),
                responsibility = request.Responsibility,
                quantityOrDuration = request.QuantityOrDuration > 0f ? request.QuantityOrDuration : source.QuantityOrDuration,
                difficulty = request.Difficulty == ProfessionalActivityDifficulty.Unknown ? source.Difficulty : request.Difficulty,
                quality = request.Quality > 0 ? request.Quality : source.Quality,
                outcomeSummary = request.OutcomeSummary ?? source.Diagnostics,
                relatedItemIds = ProfessionalActivitySourceSnapshot.Clean((request.RelatedItemIds ?? Array.Empty<string>()).Concat(source.RelatedSubjectIds ?? Array.Empty<string>())),
                relatedTargetIds = ProfessionalActivitySourceSnapshot.Clean(request.RelatedTargetIds),
                relatedOrganizationIds = ProfessionalActivitySourceSnapshot.Clean(request.RelatedOrganizationIds),
                locationId = request.LocationId ?? string.Empty,
                jobId = request.JobId ?? string.Empty,
                batchId = request.BatchId ?? string.Empty,
                experimentId = request.ExperimentId ?? string.Empty,
                evidenceReferenceIds = ProfessionalActivitySourceSnapshot.Clean(request.EvidenceReferenceIds),
                repetitionSignature = string.IsNullOrWhiteSpace(request.RepetitionSignature) ? $"{request.ActivityDefinitionId}|{source.Reference.Signature}|{string.Join(",", source.Tags)}" : request.RepetitionSignature,
                accessPolicyId = string.IsNullOrWhiteSpace(request.AccessPolicyId) ? definition.AccessPolicyId : request.AccessPolicyId,
                provenance = request.Provenance ?? string.Empty,
                revision = 1L
            };
            return true;
        }

        private void ValidateProposedActivity(ProfessionalActivityRecordData activity, ProfessionalActivityDefinition definition, ProfessionalActivitySourceSnapshot source, List<string> diagnostics)
        {
            if (!definition.ApplicableProfessionIds.Contains(activity.professionId))
            {
                diagnostics.Add("ProfessionMismatch");
            }

            if (!string.IsNullOrWhiteSpace(activity.specializationId) && definition.ApplicableSpecializationIds.Count > 0 && !definition.ApplicableSpecializationIds.Contains(activity.specializationId))
            {
                diagnostics.Add("SpecializationMismatch");
            }

            if (!definition.AcceptedSourceTypes.Contains(source.Reference.sourceType))
            {
                diagnostics.Add("MissingSource");
            }

            if (!string.Equals(source.ActingPersonId, activity.personId, StringComparison.Ordinal))
            {
                diagnostics.Add("SourceActorMismatch");
            }

            if (!source.Completed || source.Revoked || !source.AccessAllowed)
            {
                diagnostics.Add("SourceInvalidState");
            }

            if (activity.quality < definition.MinimumQuality || activity.difficulty < definition.MinimumDifficulty)
            {
                diagnostics.Add("RequirementBlocked");
            }

            if (definition.RequiredActivityTags.Count > 0 && !definition.RequiredActivityTags.All(tag => source.Tags.Contains(tag)))
            {
                diagnostics.Add("RequirementBlocked");
            }

            if (definition.SupervisionPolicy == ProfessionalSupervisionPolicy.RequiresSupervision && activity.supervisionLevel != TrainingSupervisionLevel.CloselySupervised && activity.supervisionLevel != TrainingSupervisionLevel.PeriodicallySupervised)
            {
                diagnostics.Add("RequirementBlocked");
            }

            if (definition.IndependentWorkPolicy == ProfessionalIndependentWorkPolicy.Required && activity.responsibility != ProfessionalResponsibilityLevel.IndependentPractitioner && activity.responsibility != ProfessionalResponsibilityLevel.IndependentWithReview)
            {
                diagnostics.Add("RequirementBlocked");
            }

            if ((activity.outcome == ProfessionalActivityOutcomeState.Failed || activity.outcome == ProfessionalActivityOutcomeState.DangerousMistake) && definition.FailureCreditPolicy == ProfessionalFailureCreditPolicy.NoCredit)
            {
                diagnostics.Add("RequirementBlocked");
            }

            if (definition.CreditPolicy == ProfessionalCreditPolicy.Exclusive && HasExclusiveDuplicate(activity))
            {
                diagnostics.Add("DuplicateExclusiveSource");
            }
        }

        private bool HasExclusiveDuplicate(ProfessionalActivityRecordData candidate, string ignoreActivityId = "")
        {
            return evidenceById.Values.Any(evidence => string.Equals(evidence.personId, candidate.personId, StringComparison.Ordinal)
                && string.Equals(evidence.professionId, candidate.professionId, StringComparison.Ordinal)
                && string.Equals(evidence.source?.Signature, candidate.source?.Signature, StringComparison.Ordinal)
                && !string.Equals(evidence.activityId, ignoreActivityId ?? string.Empty, StringComparison.Ordinal)
                && activitiesById.TryGetValue(evidence.activityId, out ProfessionalActivityRecordData existing)
                && existing.state == ProfessionalActivityState.Validated);
        }

        private ProfessionalExperienceEvidenceData BuildEvidence(ProfessionalActivityRecordData activity, string evidenceId, string validationAuthorityOrPolicyId, string validationWorldTime)
        {
            return new ProfessionalExperienceEvidenceData
            {
                evidenceId = evidenceId,
                activityId = activity.activityId,
                personId = activity.personId,
                professionId = activity.professionId,
                specializationId = activity.specializationId,
                source = activity.source.Clone(),
                category = ExperienceCategoryFor(activity),
                quantityOrDuration = activity.quantityOrDuration,
                difficulty = activity.difficulty,
                quality = activity.quality,
                supervisionLevel = activity.supervisionLevel,
                responsibility = activity.responsibility,
                outcome = activity.outcome,
                noveltyClassification = evidenceById.Values.Any(item => string.Equals(item.repetitionGroup, activity.repetitionSignature, StringComparison.Ordinal)) ? "Repeated" : "Novel",
                repetitionGroup = activity.repetitionSignature,
                validationAuthorityOrPolicyId = validationAuthorityOrPolicyId ?? string.Empty,
                validationWorldTime = string.IsNullOrWhiteSpace(validationWorldTime) ? activity.completionWorldTime : validationWorldTime,
                accessPolicyId = activity.accessPolicyId,
                provenance = activity.provenance,
                revision = 1L
            };
        }

        private static ProfessionalExperienceCategory ExperienceCategoryFor(ProfessionalActivityRecordData activity)
        {
            if (activity.outcome == ProfessionalActivityOutcomeState.Failed || activity.outcome == ProfessionalActivityOutcomeState.DangerousMistake)
            {
                return ProfessionalExperienceCategory.FailedAttempt;
            }

            return activity.responsibility switch
            {
                ProfessionalResponsibilityLevel.Observer => ProfessionalExperienceCategory.Observation,
                ProfessionalResponsibilityLevel.Assistant => ProfessionalExperienceCategory.AssistedWork,
                ProfessionalResponsibilityLevel.SupervisedWorker => ProfessionalExperienceCategory.SupervisedWork,
                ProfessionalResponsibilityLevel.IndependentWithReview => ProfessionalExperienceCategory.IndependentWork,
                ProfessionalResponsibilityLevel.IndependentPractitioner => ProfessionalExperienceCategory.IndependentWork,
                ProfessionalResponsibilityLevel.Supervisor => ProfessionalExperienceCategory.Leadership,
                ProfessionalResponsibilityLevel.Leader => ProfessionalExperienceCategory.Leadership,
                ProfessionalResponsibilityLevel.Instructor => ProfessionalExperienceCategory.Teaching,
                _ => activity.category == ProfessionalActivityCategory.Experimentation || activity.category == ProfessionalActivityCategory.Research ? ProfessionalExperienceCategory.Research : ProfessionalExperienceCategory.RoutineWork
            };
        }

        private ProfessionalActivityOperationResult SetActivityState(string activityId, ProfessionalActivityState state, string transactionId, string reason, ProfessionalActivityHistoryHookKind hookKind = ProfessionalActivityHistoryHookKind.ExperienceRecordCorrected)
        {
            long prior = revision;
            if (!activitiesById.TryGetValue(activityId ?? string.Empty, out ProfessionalActivityRecordData activity))
            {
                return ProfessionalActivityOperationResult.Failure(ProfessionalActivityOperationStatus.MissingActivity, "Professional activity is missing.", revision);
            }

            activity.state = state;
            activity.outcomeSummary = string.IsNullOrWhiteSpace(reason) ? activity.outcomeSummary : reason;
            activity.revision++;
            activitiesById[activity.activityId] = activity.Clone();
            revision++;
            AddHook(hookKind, activity, string.Empty, transactionId);
            return ProfessionalActivityOperationResult.Success($"Professional activity state changed to {state}.", prior, revision, activity);
        }

        private void AddHook(ProfessionalActivityHistoryHookKind kind, ProfessionalActivityRecordData activity, string evidenceId, string transactionId)
        {
            if (activity == null)
            {
                return;
            }

            historyHooks.Add(new ProfessionalActivityHistoryHookData
            {
                kind = kind,
                activityId = activity.activityId,
                evidenceId = evidenceId ?? string.Empty,
                personId = activity.personId,
                professionId = activity.professionId,
                worldTime = activity.completionWorldTime,
                transactionId = transactionId ?? string.Empty
            });
        }

        private static ProfessionalActivityHistoryHookKind HookForEvidence(ProfessionalExperienceEvidenceData evidence)
        {
            if (evidence.category == ProfessionalExperienceCategory.Innovation || evidence.difficulty == ProfessionalActivityDifficulty.Innovative)
            {
                return ProfessionalActivityHistoryHookKind.ImportantInnovation;
            }

            if (evidence.category == ProfessionalExperienceCategory.FailedAttempt)
            {
                return ProfessionalActivityHistoryHookKind.ImportantFailure;
            }

            if (evidence.responsibility == ProfessionalResponsibilityLevel.Leader || evidence.responsibility == ProfessionalResponsibilityLevel.Supervisor)
            {
                return ProfessionalActivityHistoryHookKind.LeadershipOfMajorWork;
            }

            if (evidence.responsibility == ProfessionalResponsibilityLevel.IndependentPractitioner)
            {
                return ProfessionalActivityHistoryHookKind.MajorIndependentWork;
            }

            return ProfessionalActivityHistoryHookKind.FirstProfessionalActivity;
        }

        private static ProfessionalActivityOperationStatus MapDiagnosticStatus(IReadOnlyList<string> diagnostics)
        {
            if (diagnostics.Contains("MissingRuntime")) return ProfessionalActivityOperationStatus.MissingRuntime;
            if (diagnostics.Contains("MissingDefinition")) return ProfessionalActivityOperationStatus.MissingDefinition;
            if (diagnostics.Contains("MissingPerson")) return ProfessionalActivityOperationStatus.MissingPerson;
            if (diagnostics.Contains("MissingProfession")) return ProfessionalActivityOperationStatus.MissingProfession;
            if (diagnostics.Contains("MissingProfessionRelationship")) return ProfessionalActivityOperationStatus.MissingProfessionRelationship;
            if (diagnostics.Contains("MissingSource")) return ProfessionalActivityOperationStatus.MissingSource;
            if (diagnostics.Contains("SourceActorMismatch")) return ProfessionalActivityOperationStatus.SourceActorMismatch;
            if (diagnostics.Contains("SourceInvalidState")) return ProfessionalActivityOperationStatus.SourceInvalidState;
            if (diagnostics.Contains("ProfessionMismatch")) return ProfessionalActivityOperationStatus.ProfessionMismatch;
            if (diagnostics.Contains("SpecializationMismatch")) return ProfessionalActivityOperationStatus.SpecializationMismatch;
            if (diagnostics.Contains("DuplicateExclusiveSource")) return ProfessionalActivityOperationStatus.DuplicateExclusiveSource;
            if (diagnostics.Contains("RequirementBlocked")) return ProfessionalActivityOperationStatus.RequirementBlocked;
            return ProfessionalActivityOperationStatus.InvalidRequest;
        }

        private static bool HasProfessionRelationship(PersonProfessionRuntime professions, string personId, string professionId, bool activeOnly)
        {
            return professions != null
                && professions.QueryByProfession(professionId ?? string.Empty, activeOnly)
                    .Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal));
        }
    }

    public sealed class ProfessionalActivityRegistrationRequest
    {
        public string ActivityId { get; set; }
        public string PersonId { get; set; }
        public string ProfessionId { get; set; }
        public string SpecializationId { get; set; }
        public string ActivityDefinitionId { get; set; }
        public ProfessionalActivitySourceSnapshot Source { get; set; }
        public ProfessionalActivitySourceReferenceData SourceReference { get; set; }
        public ProfessionalActivityCategory Category { get; set; } = ProfessionalActivityCategory.Custom;
        public string StartWorldTime { get; set; }
        public string CompletionWorldTime { get; set; }
        public ProfessionalActivityOutcomeState Outcome { get; set; } = ProfessionalActivityOutcomeState.Unknown;
        public TrainingSupervisionLevel SupervisionLevel { get; set; } = TrainingSupervisionLevel.Custom;
        public IEnumerable<string> SupervisorOrInstructorIds { get; set; } = Array.Empty<string>();
        public ProfessionalResponsibilityLevel Responsibility { get; set; } = ProfessionalResponsibilityLevel.Observer;
        public float QuantityOrDuration { get; set; }
        public ProfessionalActivityDifficulty Difficulty { get; set; } = ProfessionalActivityDifficulty.Unknown;
        public int Quality { get; set; }
        public string OutcomeSummary { get; set; }
        public IEnumerable<string> RelatedItemIds { get; set; } = Array.Empty<string>();
        public IEnumerable<string> RelatedTargetIds { get; set; } = Array.Empty<string>();
        public IEnumerable<string> RelatedOrganizationIds { get; set; } = Array.Empty<string>();
        public string LocationId { get; set; }
        public string JobId { get; set; }
        public string BatchId { get; set; }
        public string ExperimentId { get; set; }
        public IEnumerable<string> EvidenceReferenceIds { get; set; } = Array.Empty<string>();
        public string RepetitionSignature { get; set; }
        public string AccessPolicyId { get; set; }
        public string Provenance { get; set; }
    }
}
