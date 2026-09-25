using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Professions.Integration
{
    public static class Step10IntegrationValidator
    {
        public static readonly IReadOnlyList<Step10IntegrationAuthorityEntry> AuthorityMap = new[]
        {
            new Step10IntegrationAuthorityEntry("profession.relationship", "PersonProfessionRuntime", "ProfessionEntryRuntime", "TrainingRuntime", "ProfessionalActivityRuntime", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("profession.entry", "ProfessionEntryRuntime", "PersonProfessionRuntime"),
            new Step10IntegrationAuthorityEntry("training.enrollment", "TrainingRuntime", "CredentialRuntime", "ProfessionalRankRuntime", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("professional.activity", "ProfessionalActivityRuntime", "CredentialRuntime", "ProfessionalRankRuntime", "TrainingRuntime", "PositionEmploymentRuntime", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("credential.record", "CredentialRuntime", "ProfessionalRankRuntime", "PositionEmploymentRuntime", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("professional.rank", "ProfessionalRankRuntime", "PositionEmploymentRuntime", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("position.employment", "PositionEmploymentRuntime", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("career.history", "CareerHistoryRuntime", "LifePathRuntime"),
            new Step10IntegrationAuthorityEntry("life.path", "LifePathRuntime", "PersonProfessionRuntime", "TrainingRuntime", "CredentialRuntime", "ProfessionalRankRuntime", "PositionEmploymentRuntime", "CareerHistoryRuntime")
        };

        public static readonly IReadOnlyList<Step10IntegrationDependencyEntry> PersistenceDependencies = new[]
        {
            new Step10IntegrationDependencyEntry("PersonProfessionRuntime"),
            new Step10IntegrationDependencyEntry("ProfessionEntryRuntime", "PersonProfessionRuntime"),
            new Step10IntegrationDependencyEntry("TrainingRuntime", "PersonProfessionRuntime"),
            new Step10IntegrationDependencyEntry("ProfessionalActivityRuntime", "PersonProfessionRuntime"),
            new Step10IntegrationDependencyEntry("CredentialRuntime", "PersonProfessionRuntime", "TrainingRuntime", "ProfessionalActivityRuntime"),
            new Step10IntegrationDependencyEntry("ProfessionalRankRuntime", "PersonProfessionRuntime", "TrainingRuntime", "ProfessionalActivityRuntime", "CredentialRuntime"),
            new Step10IntegrationDependencyEntry("PositionEmploymentRuntime", "PersonProfessionRuntime", "TrainingRuntime", "ProfessionalActivityRuntime", "CredentialRuntime", "ProfessionalRankRuntime"),
            new Step10IntegrationDependencyEntry("CareerHistoryRuntime", "PersonProfessionRuntime", "TrainingRuntime", "ProfessionalActivityRuntime", "CredentialRuntime", "ProfessionalRankRuntime", "PositionEmploymentRuntime"),
            new Step10IntegrationDependencyEntry("LifePathRuntime", "PersonProfessionRuntime", "TrainingRuntime", "ProfessionalActivityRuntime", "CredentialRuntime", "ProfessionalRankRuntime", "PositionEmploymentRuntime", "CareerHistoryRuntime")
        };

        public static Step10IntegrationValidationReport ValidateDefinitions(DefinitionRegistry registry)
        {
            Step10IntegrationValidationReport report = new Step10IntegrationValidationReport();
            ValidateAuthorityMap(report);
            ValidatePersistenceDependencies(report);

            if (registry == null)
            {
                report.AddError(Step10IntegrationDiagnosticDomain.DefinitionCatalog, "MissingRegistry", "Step 10 integration validation requires a definition registry.");
                return report;
            }

            string[] requiredDefinitionTypes =
            {
                nameof(ProfessionDefinition),
                nameof(ProfessionSpecializationDefinition),
                nameof(ProfessionEntryPathDefinition),
                nameof(TrainingProgramDefinition),
                nameof(TrainingCurriculumDefinition),
                nameof(ProfessionalActivityDefinition),
                nameof(CredentialDefinition),
                nameof(CredentialExaminationDefinition),
                nameof(ProfessionalRankDefinition),
                nameof(ProfessionalRankLadderDefinition),
                nameof(ProfessionalMasteryDefinition),
                nameof(PositionDefinition),
                nameof(DutyDefinition),
                nameof(CareerTransitionDefinition),
                nameof(AspirationDefinition),
                nameof(LifeGoalDefinition)
            };

            HashSet<string> presentTypes = registry.DefinitionsById.Values
                .Where(definition => definition != null)
                .Select(definition => definition.GetType().Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string typeName in requiredDefinitionTypes)
            {
                if (!presentTypes.Contains(typeName))
                {
                    report.AddWarning(Step10IntegrationDiagnosticDomain.DefinitionCatalog, "MissingRepresentativeType", $"No catalog definition of type '{typeName}' is registered.", typeName);
                }
            }

            return report;
        }

        public static Step10IntegrationValidationReport ValidateRuntimeGraph(
            Step10IntegrationRuntimeSnapshot snapshot,
            DefinitionRegistry registry = null,
            IEnumerable<string> knownPersonIds = null,
            IEnumerable<string> knownOrganizationIds = null,
            IEnumerable<string> knownAuthorityIds = null)
        {
            Step10IntegrationValidationReport report = ValidateDefinitions(registry);
            Step10IntegrationRuntimeSnapshot data = snapshot?.Clone() ?? new Step10IntegrationRuntimeSnapshot();
            HashSet<string> persons = CleanSet(knownPersonIds);
            HashSet<string> organizations = CleanSet(knownOrganizationIds);
            HashSet<string> authorities = CleanSet(knownAuthorityIds);

            ValidateSaveSchemas(data, report);

            Dictionary<string, PersonProfessionRelationshipData> professions = ValidateProfessions(data.Professions, registry, persons, authorities, report);
            Dictionary<string, ProfessionEntryRequestData> entries = ValidateEntries(data.Entries, registry, persons, professions, authorities, report);
            Dictionary<string, TrainingEnrollmentData> training = ValidateTraining(data.Training, registry, persons, professions, report);
            Dictionary<string, ProfessionalActivityRecordData> activities = ValidateActivities(data.Activities, registry, persons, professions, report);
            Dictionary<string, ProfessionalExperienceEvidenceData> evidence = ValidateExperienceEvidence(data.Activities, registry, persons, activities, report);
            Dictionary<string, CredentialRecordData> credentials = ValidateCredentials(data.Credentials, registry, persons, authorities, training, evidence, report);
            Dictionary<string, ProfessionalRankRecordData> ranks = ValidateRanks(data.Ranks, registry, persons, authorities, professions, credentials, evidence, report);
            Dictionary<string, PositionInstanceData> positions = ValidatePositions(data.Positions, registry, organizations, report);
            Dictionary<string, EmploymentRecordData> employments = ValidateEmployments(data.Positions, registry, persons, organizations, authorities, positions, ranks, credentials, training, evidence, report);
            Dictionary<string, DutyAssignmentData> duties = ValidateDuties(data.Positions, registry, persons, employments, positions, activities, report);
            ValidatePositionHolderConsistency(positions, employments, duties, report);
            ValidateReportingCycles(positions, report);
            ValidateCareerHistory(data.CareerHistory, registry, persons, organizations, professions, training, activities, evidence, credentials, ranks, positions, employments, report);
            ValidateLifePaths(data.LifePaths, registry, persons, organizations, professions, training, activities, credentials, ranks, positions, employments, data.CareerHistory, report);
            ValidateAccessLeakage(data, report);
            ValidateFingerprintDeterminism(data, report);

            return report;
        }

        public static string CreateCanonicalFingerprint(Step10IntegrationRuntimeSnapshot snapshot)
        {
            Step10IntegrationRuntimeSnapshot data = snapshot?.Clone() ?? new Step10IntegrationRuntimeSnapshot();
            StringBuilder builder = new StringBuilder(8192);

            AppendSection(builder, "professions", data.Professions.relationships, item => item.relationshipId, item =>
                $"{item.relationshipId}|{item.personId}|{item.professionId}|{item.state}|{item.active}|{item.primary}|{item.formalPractice}|{item.recognized}|{string.Join(",", Sorted(item.specializationIds))}|{item.revision}");
            AppendSection(builder, "entry", data.Entries.requests, item => item.requestId, item =>
                $"{item.requestId}|{item.applicantPersonId}|{item.professionId}|{item.entryPathId}|{item.specializationId}|{item.authorityId}|{item.state}|{item.relationshipId}|{item.revision}");
            AppendSection(builder, "training", data.Training.enrollments, item => item.enrollmentId, item =>
                $"{item.enrollmentId}|{item.personId}|{item.programId}|{item.relatedProfessionId}|{item.relatedSpecializationId}|{item.state}|{string.Join(",", Sorted(item.completedModuleIds))}|{item.revision}");
            AppendSection(builder, "activities", data.Activities.activities, item => item.activityId, item =>
                $"{item.activityId}|{item.personId}|{item.professionId}|{item.specializationId}|{item.activityDefinitionId}|{item.state}|{item.outcome}|{item.source?.Signature}|{item.revision}");
            AppendSection(builder, "evidence", data.Activities.evidence, item => item.evidenceId, item =>
                $"{item.evidenceId}|{item.activityId}|{item.personId}|{item.professionId}|{item.category}|{item.outcome}|{item.revision}");
            AppendSection(builder, "credentials", data.Credentials.credentials, item => item.credentialId, item =>
                $"{item.credentialId}|{item.credentialDefinitionId}|{item.recipientPersonId}|{item.state}|{item.authenticityState}|{item.issuer?.Signature}|{item.supportingApplicationId}|{item.supportingExaminationAttemptId}|{item.revision}");
            AppendSection(builder, "ranks", data.Ranks.ranks, item => item.rankRecordId, item =>
                $"{item.rankRecordId}|{item.personId}|{item.professionId}|{item.specializationId}|{item.ladderDefinitionId}|{item.rankDefinitionId}|{item.state}|{item.supportingApplicationId}|{item.revision}");
            AppendSection(builder, "masteries", data.Ranks.masteries, item => item.masteryRecordId, item =>
                $"{item.masteryRecordId}|{item.personId}|{item.professionId}|{item.masteryDefinitionId}|{item.state}|{item.revision}");
            AppendSection(builder, "positions", data.Positions.positions, item => item.positionInstanceId, item =>
                $"{item.positionInstanceId}|{item.positionDefinitionId}|{item.organizationId}|{item.state}|{string.Join(",", Sorted(item.holderPersonIds))}|{item.supervisorPositionInstanceId}|{item.revision}");
            AppendSection(builder, "employments", data.Positions.employments, item => item.employmentId, item =>
                $"{item.employmentId}|{item.personId}|{item.employerOrganizationId}|{item.positionInstanceId}|{item.positionDefinitionId}|{item.state}|{string.Join(",", Sorted(item.dutyAssignmentIds))}|{item.revision}");
            AppendSection(builder, "duties", data.Positions.duties, item => item.assignmentId, item =>
                $"{item.assignmentId}|{item.employmentId}|{item.positionInstanceId}|{item.dutyDefinitionId}|{item.assignedPersonId}|{item.state}|{string.Join(",", Sorted(item.completionEvidenceReferenceIds))}|{item.revision}");
            AppendSection(builder, "career-episodes", data.CareerHistory.episodes, item => item.episodeId, item =>
                $"{item.episodeId}|{item.personId}|{item.professionId}|{item.employmentId}|{item.positionInstanceId}|{item.state}|{item.category}|{item.primaryCareer}|{item.startWorldTime}|{item.endWorldTime}|{item.revision}");
            AppendSection(builder, "career-transitions", data.CareerHistory.transitions, item => item.transitionId, item =>
                $"{item.transitionId}|{item.personId}|{item.transitionDefinitionId}|{item.category}|{item.professionId}|{item.previousEmploymentId}|{item.newEmploymentId}|{item.transitionWorldTime}|{item.revision}");
            AppendSection(builder, "life-paths", data.LifePaths.lifePaths, item => item.lifePathId, item =>
                $"{item.lifePathId}|{item.personId}|{item.state}|{string.Join(",", Sorted(item.professionRelationshipIds))}|{string.Join(",", Sorted(item.activeAspirationIds))}|{string.Join(",", Sorted(item.activeGoalIds))}|{item.primaryProfessionalIdentityId}|{item.revision}");
            AppendSection(builder, "aspirations", data.LifePaths.aspirations, item => item.aspirationId, item =>
                $"{item.aspirationId}|{item.personId}|{item.aspirationDefinitionId}|{item.state}|{item.targetProfessionId}|{item.targetRankDefinitionId}|{item.targetCredentialDefinitionId}|{string.Join(",", Sorted(item.relatedGoalIds))}|{item.revision}");
            AppendSection(builder, "goals", data.LifePaths.goals, item => item.goalId, item =>
                $"{item.goalId}|{item.personId}|{item.goalDefinitionId}|{item.parentAspirationId}|{item.state}|{item.progressState}|{item.targetProfessionId}|{item.targetTrainingProgramId}|{item.targetCredentialDefinitionId}|{item.targetRankDefinitionId}|{item.targetPositionDefinitionId}|{item.targetActivityDefinitionId}|{string.Join(",", Sorted(item.completedRequirementIds))}|{string.Join(",", Sorted(item.remainingRequirementIds))}|{item.revision}");
            AppendSection(builder, "identities", data.LifePaths.identities, item => item.identityId, item =>
                $"{item.identityId}|{item.personId}|{item.kind}|{item.alignment}|{item.professionId}|{item.professionRelationshipId}|{item.careerEpisodeId}|{item.active}|{item.secret}|{item.revision}");

            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static Dictionary<string, PersonProfessionRelationshipData> ValidateProfessions(PersonProfessionRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, HashSet<string> authorities, Step10IntegrationValidationReport report)
        {
            Dictionary<string, PersonProfessionRelationshipData> relationships = Index(save.relationships, item => item.relationshipId, "ProfessionRelationship", report);

            foreach (PersonProfessionRelationshipData relationship in save.relationships ?? new List<PersonProfessionRelationshipData>())
            {
                if (relationship == null)
                {
                    continue;
                }

                ReferencePerson(persons, relationship.personId, relationship.relationshipId, "UnknownProfessionPerson", report, "Professions.relationships.personId");
                ReferenceDefinition<ProfessionDefinition>(registry, relationship.professionId, relationship.relationshipId, "MissingProfessionDefinition", report, "Professions.relationships.professionId");
                foreach (string specializationId in relationship.specializationIds ?? Array.Empty<string>())
                {
                    if (ReferenceDefinition<ProfessionSpecializationDefinition>(registry, specializationId, relationship.relationshipId, "MissingSpecializationDefinition", report, "Professions.relationships.specializationIds")
                        && registry.TryGet(specializationId, out ProfessionSpecializationDefinition specialization)
                        && !Same(specialization.ParentProfessionId, relationship.professionId))
                    {
                        report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "SpecializationWithoutParentProfession", $"Specialization '{specializationId}' belongs to '{specialization.ParentProfessionId}', not '{relationship.professionId}'.", relationship.relationshipId, "ProfessionRelationship -> ProfessionSpecializationDefinition.ParentProfessionId");
                    }
                }

                if (relationship.recognized && string.IsNullOrWhiteSpace(relationship.recognizingAuthorityId))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "FormalRecognitionMissingAuthority", "Recognized profession relationships must identify the recognizing authority.", relationship.relationshipId, "ProfessionRelationship.recognizingAuthorityId");
                }
                else if (relationship.recognized)
                {
                    ReferenceAuthority(authorities, relationship.recognizingAuthorityId, relationship.relationshipId, "UnknownRecognitionAuthority", report, "ProfessionRelationship.recognizingAuthorityId");
                }
            }

            foreach (IGrouping<string, PersonProfessionRelationshipData> group in save.relationships
                .Where(item => item != null && item.active && !TerminalProfession(item.state))
                .GroupBy(item => $"{item.personId}|{item.professionId}", StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "DuplicateActiveProfessionRelationship", "A Person cannot have duplicate active relationships to the same profession.", group.First().personId, "Person -> ProfessionRelationship");
                }
            }

            foreach (IGrouping<string, PersonProfessionRelationshipData> group in save.relationships
                .Where(item => item != null && item.active && item.primary && !TerminalProfession(item.state))
                .GroupBy(item => item.personId ?? string.Empty, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.Lifecycle, "MultiplePrimaryProfessions", "A Person cannot have multiple active primary professions unless a later policy explicitly allows it.", group.Key, "Person -> Primary ProfessionRelationship");
                }
            }

            return relationships;
        }

        private static Dictionary<string, ProfessionEntryRequestData> ValidateEntries(ProfessionEntryRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, Dictionary<string, PersonProfessionRelationshipData> professions, HashSet<string> authorities, Step10IntegrationValidationReport report)
        {
            Dictionary<string, ProfessionEntryRequestData> requests = Index(save.requests, item => item.requestId, "ProfessionEntryRequest", report);
            foreach (ProfessionEntryRequestData request in save.requests ?? new List<ProfessionEntryRequestData>())
            {
                if (request == null)
                {
                    continue;
                }

                ReferencePerson(persons, request.applicantPersonId, request.requestId, "UnknownEntryApplicant", report, "EntryRequest.applicantPersonId");
                ReferenceDefinition<ProfessionDefinition>(registry, request.professionId, request.requestId, "MissingEntryProfessionDefinition", report, "EntryRequest.professionId");
                ReferenceDefinition<ProfessionEntryPathDefinition>(registry, request.entryPathId, request.requestId, "MissingEntryPathDefinition", report, "EntryRequest.entryPathId");
                ReferenceAuthority(authorities, request.authorityId, request.requestId, "UnknownEntryAuthority", report, "EntryRequest.authorityId", allowEmpty: true);
                if (!string.IsNullOrWhiteSpace(request.relationshipId) && !professions.ContainsKey(request.relationshipId))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "EntryRelationshipMissing", $"Entry request references missing profession relationship '{request.relationshipId}'.", request.requestId, "EntryRequest.relationshipId -> ProfessionRelationship.relationshipId");
                }
            }

            return requests;
        }

        private static Dictionary<string, TrainingEnrollmentData> ValidateTraining(TrainingRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, Dictionary<string, PersonProfessionRelationshipData> professions, Step10IntegrationValidationReport report)
        {
            Dictionary<string, TrainingEnrollmentData> enrollments = Index(save.enrollments, item => item.enrollmentId, "TrainingEnrollment", report);
            foreach (TrainingEnrollmentData enrollment in save.enrollments ?? new List<TrainingEnrollmentData>())
            {
                if (enrollment == null)
                {
                    continue;
                }

                ReferencePerson(persons, enrollment.personId, enrollment.enrollmentId, "UnknownTrainingLearner", report, "TrainingEnrollment.personId");
                ReferenceDefinition<TrainingProgramDefinition>(registry, enrollment.programId, enrollment.enrollmentId, "MissingTrainingProgramDefinition", report, "TrainingEnrollment.programId");
                ReferenceDefinition<ProfessionDefinition>(registry, enrollment.relatedProfessionId, enrollment.enrollmentId, "MissingTrainingProfessionDefinition", report, "TrainingEnrollment.relatedProfessionId", allowEmpty: true);
                if (!string.IsNullOrWhiteSpace(enrollment.relatedSpecializationId))
                {
                    ReferenceDefinition<ProfessionSpecializationDefinition>(registry, enrollment.relatedSpecializationId, enrollment.enrollmentId, "MissingTrainingSpecializationDefinition", report, "TrainingEnrollment.relatedSpecializationId");
                }
            }

            return enrollments;
        }

        private static Dictionary<string, ProfessionalActivityRecordData> ValidateActivities(ProfessionalActivityRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, Dictionary<string, PersonProfessionRelationshipData> professions, Step10IntegrationValidationReport report)
        {
            Dictionary<string, ProfessionalActivityRecordData> activities = Index(save.activities, item => item.activityId, "ProfessionalActivity", report);
            foreach (ProfessionalActivityRecordData activity in save.activities ?? new List<ProfessionalActivityRecordData>())
            {
                if (activity == null)
                {
                    continue;
                }

                ReferencePerson(persons, activity.personId, activity.activityId, "UnknownActivityPerson", report, "ProfessionalActivity.personId");
                ReferenceDefinition<ProfessionalActivityDefinition>(registry, activity.activityDefinitionId, activity.activityId, "MissingActivityDefinition", report, "ProfessionalActivity.activityDefinitionId");
                ReferenceDefinition<ProfessionDefinition>(registry, activity.professionId, activity.activityId, "MissingActivityProfessionDefinition", report, "ProfessionalActivity.professionId");
                if (!professions.Values.Any(item => Same(item.personId, activity.personId) && Same(item.professionId, activity.professionId) && item.active && !TerminalProfession(item.state)))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "ActivityMissingProfessionRelationship", "Professional activity must reference an existing active profession relationship for the acting Person.", activity.activityId, "ProfessionalActivity -> ProfessionRelationship");
                }
            }

            return activities;
        }

        private static Dictionary<string, ProfessionalExperienceEvidenceData> ValidateExperienceEvidence(ProfessionalActivityRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, Dictionary<string, ProfessionalActivityRecordData> activities, Step10IntegrationValidationReport report)
        {
            Dictionary<string, ProfessionalExperienceEvidenceData> evidence = Index(save.evidence, item => item.evidenceId, "ExperienceEvidence", report);
            foreach (ProfessionalExperienceEvidenceData item in save.evidence ?? new List<ProfessionalExperienceEvidenceData>())
            {
                if (item == null)
                {
                    continue;
                }

                ReferencePerson(persons, item.personId, item.evidenceId, "UnknownEvidencePerson", report, "ExperienceEvidence.personId");
                if (!ReferenceRecord(activities, item.activityId, item.evidenceId, "EvidenceActivityMissing", report, "ExperienceEvidence.activityId -> ProfessionalActivity.activityId", allowEmpty: false)
                    || !activities.TryGetValue(item.activityId, out ProfessionalActivityRecordData activity)
                    || !Same(activity.personId, item.personId))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "EvidenceActivityPersonMismatch", "Experience evidence must belong to the same Person as its activity.", item.evidenceId, "ExperienceEvidence -> ProfessionalActivity.personId");
                }
            }

            return evidence;
        }

        private static Dictionary<string, CredentialRecordData> ValidateCredentials(CredentialRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, HashSet<string> authorities, Dictionary<string, TrainingEnrollmentData> training, Dictionary<string, ProfessionalExperienceEvidenceData> evidence, Step10IntegrationValidationReport report)
        {
            Index(save.applications, item => item.applicationId, "CredentialApplication", report);
            Index(save.examinationAttempts, item => item.attemptId, "CredentialExaminationAttempt", report);
            Dictionary<string, CredentialRecordData> credentials = Index(save.credentials, item => item.credentialId, "Credential", report);

            foreach (CredentialRecordData credential in save.credentials ?? new List<CredentialRecordData>())
            {
                if (credential == null)
                {
                    continue;
                }

                ReferencePerson(persons, credential.recipientPersonId, credential.credentialId, "UnknownCredentialRecipient", report, "Credential.recipientPersonId");
                ReferenceDefinition<CredentialDefinition>(registry, credential.credentialDefinitionId, credential.credentialId, "MissingCredentialDefinition", report, "Credential.credentialDefinitionId");
                if (credential.issuer != null)
                {
                    ReferenceAuthority(authorities, credential.issuer.issuerId, credential.credentialId, "UnknownCredentialIssuer", report, "Credential.issuer.issuerId", allowEmpty: false);
                }
                else
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "CredentialMissingIssuer", "Credential records must retain their authoritative issuer.", credential.credentialId, "Credential.issuer");
                }

                foreach (string trainingId in credential.supportingTrainingRecordIds ?? Array.Empty<string>())
                {
                    bool matchesTrainingRecord = ReferenceRecord(
                        training,
                        trainingId,
                        credential.credentialId,
                        "CredentialTrainingMissing",
                        report,
                        "Credential.supportingTrainingRecordIds -> TrainingEnrollment.enrollmentId",
                        allowEmpty: true,
                        addError: false);
                    bool matchesTrainingDefinition = ReferenceDefinition<TrainingProgramDefinition>(
                        registry,
                        trainingId,
                        credential.credentialId,
                        "CredentialTrainingProgramMissing",
                        report,
                        "Credential.supportingTrainingRecordIds -> TrainingProgramDefinition.Id",
                        allowEmpty: true,
                        addError: false);
                    if (!matchesTrainingRecord && !matchesTrainingDefinition && !string.IsNullOrWhiteSpace(trainingId))
                    {
                        report.AddError(
                            Step10IntegrationDiagnosticDomain.PersonGraph,
                            "CredentialTrainingMissing",
                            $"Credential training support '{trainingId}' does not resolve to a training enrollment or training program definition.",
                            credential.credentialId,
                            "Credential.supportingTrainingRecordIds");
                    }
                }

                foreach (string evidenceId in credential.supportingExperienceEvidenceIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(evidence, evidenceId, credential.credentialId, "CredentialEvidenceMissing", report, "Credential.supportingExperienceEvidenceIds -> ExperienceEvidence.evidenceId", allowEmpty: true);
                }
            }

            return credentials;
        }

        private static Dictionary<string, ProfessionalRankRecordData> ValidateRanks(ProfessionalRankRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, HashSet<string> authorities, Dictionary<string, PersonProfessionRelationshipData> professions, Dictionary<string, CredentialRecordData> credentials, Dictionary<string, ProfessionalExperienceEvidenceData> evidence, Step10IntegrationValidationReport report)
        {
            Index(save.applications, item => item.applicationId, "RankApplication", report);
            Dictionary<string, ProfessionalRankRecordData> ranks = Index(save.ranks, item => item.rankRecordId, "ProfessionalRank", report);
            Index(save.masteries, item => item.masteryRecordId, "ProfessionalMastery", report);
            Index(save.achievements, item => item.achievementId, "ProfessionalAchievement", report);

            foreach (ProfessionalRankRecordData rank in save.ranks ?? new List<ProfessionalRankRecordData>())
            {
                if (rank == null)
                {
                    continue;
                }

                ReferencePerson(persons, rank.personId, rank.rankRecordId, "UnknownRankPerson", report, "Rank.personId");
                ReferenceDefinition<ProfessionDefinition>(registry, rank.professionId, rank.rankRecordId, "MissingRankProfessionDefinition", report, "Rank.professionId");
                ReferenceDefinition<ProfessionalRankDefinition>(registry, rank.rankDefinitionId, rank.rankRecordId, "MissingRankDefinition", report, "Rank.rankDefinitionId");
                ReferenceDefinition<ProfessionalRankLadderDefinition>(registry, rank.ladderDefinitionId, rank.rankRecordId, "MissingRankLadderDefinition", report, "Rank.ladderDefinitionId", allowEmpty: true);
                ReferenceAuthority(authorities, rank.recognizingAuthorityId, rank.rankRecordId, "UnknownRankAuthority", report, "Rank.recognizingAuthorityId", allowEmpty: true);

                if (!professions.Values.Any(item => Same(item.personId, rank.personId) && Same(item.professionId, rank.professionId) && item.active && !TerminalProfession(item.state)))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "RankMissingProfessionRelationship", "Rank record must reference a profession currently or historically owned by PersonProfessionRuntime.", rank.rankRecordId, "Rank -> ProfessionRelationship");
                }

                foreach (string credentialId in rank.supportingCredentialIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(credentials, credentialId, rank.rankRecordId, "RankCredentialMissing", report, "Rank.supportingCredentialIds -> Credential.credentialId", allowEmpty: true);
                }

                foreach (string evidenceId in rank.supportingExperienceEvidenceIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(evidence, evidenceId, rank.rankRecordId, "RankEvidenceMissing", report, "Rank.supportingExperienceEvidenceIds -> ExperienceEvidence.evidenceId", allowEmpty: true);
                }
            }

            return ranks;
        }

        private static Dictionary<string, PositionInstanceData> ValidatePositions(PositionEmploymentRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> organizations, Step10IntegrationValidationReport report)
        {
            Dictionary<string, PositionInstanceData> positions = Index(save.positions, item => item.positionInstanceId, "PositionInstance", report);
            foreach (PositionInstanceData position in save.positions ?? new List<PositionInstanceData>())
            {
                if (position == null)
                {
                    continue;
                }

                ReferenceDefinition<PositionDefinition>(registry, position.positionDefinitionId, position.positionInstanceId, "MissingPositionDefinition", report, "Position.positionDefinitionId");
                ReferenceOrganization(organizations, position.organizationId, position.positionInstanceId, "UnknownPositionOrganization", report, "Position.organizationId");
            }

            return positions;
        }

        private static Dictionary<string, EmploymentRecordData> ValidateEmployments(PositionEmploymentRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, HashSet<string> organizations, HashSet<string> authorities, Dictionary<string, PositionInstanceData> positions, Dictionary<string, ProfessionalRankRecordData> ranks, Dictionary<string, CredentialRecordData> credentials, Dictionary<string, TrainingEnrollmentData> training, Dictionary<string, ProfessionalExperienceEvidenceData> evidence, Step10IntegrationValidationReport report)
        {
            Index(save.applications, item => item.requestId, "PositionApplication", report);
            Dictionary<string, EmploymentRecordData> employments = Index(save.employments, item => item.employmentId, "Employment", report);
            foreach (EmploymentRecordData employment in save.employments ?? new List<EmploymentRecordData>())
            {
                if (employment == null)
                {
                    continue;
                }

                ReferencePerson(persons, employment.personId, employment.employmentId, "UnknownEmploymentPerson", report, "Employment.personId");
                ReferenceOrganization(organizations, employment.employerOrganizationId, employment.employmentId, "UnknownEmploymentOrganization", report, "Employment.employerOrganizationId");
                ReferenceRecord(positions, employment.positionInstanceId, employment.employmentId, "EmploymentPositionMissing", report, "Employment.positionInstanceId -> Position.positionInstanceId");
                ReferenceDefinition<PositionDefinition>(registry, employment.positionDefinitionId, employment.employmentId, "MissingEmploymentPositionDefinition", report, "Employment.positionDefinitionId");
                ReferenceAuthority(authorities, employment.appointmentAuthorityId, employment.employmentId, "UnknownAppointmentAuthority", report, "Employment.appointmentAuthorityId", allowEmpty: true);
            }

            return employments;
        }

        private static Dictionary<string, DutyAssignmentData> ValidateDuties(PositionEmploymentRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, Dictionary<string, EmploymentRecordData> employments, Dictionary<string, PositionInstanceData> positions, Dictionary<string, ProfessionalActivityRecordData> activities, Step10IntegrationValidationReport report)
        {
            Dictionary<string, DutyAssignmentData> duties = Index(save.duties, item => item.assignmentId, "DutyAssignment", report);
            foreach (DutyAssignmentData duty in save.duties ?? new List<DutyAssignmentData>())
            {
                if (duty == null)
                {
                    continue;
                }

                ReferencePerson(persons, duty.assignedPersonId, duty.assignmentId, "UnknownDutyAssignee", report, "Duty.assignedPersonId");
                ReferenceRecord(employments, duty.employmentId, duty.assignmentId, "DutyEmploymentMissing", report, "Duty.employmentId -> Employment.employmentId");
                ReferenceRecord(positions, duty.positionInstanceId, duty.assignmentId, "DutyPositionMissing", report, "Duty.positionInstanceId -> Position.positionInstanceId");
                ReferenceDefinition<DutyDefinition>(registry, duty.dutyDefinitionId, duty.assignmentId, "MissingDutyDefinition", report, "Duty.dutyDefinitionId");
                foreach (string evidenceId in duty.completionEvidenceReferenceIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(activities, evidenceId, duty.assignmentId, "DutyEvidenceMissing", report, "Duty.completionEvidenceReferenceIds -> ProfessionalActivity.activityId", allowEmpty: true);
                }
            }

            return duties;
        }

        private static void ValidatePositionHolderConsistency(Dictionary<string, PositionInstanceData> positions, Dictionary<string, EmploymentRecordData> employments, Dictionary<string, DutyAssignmentData> duties, Step10IntegrationValidationReport report)
        {
            foreach (EmploymentRecordData employment in employments.Values)
            {
                if (!ActiveEmployment(employment.state))
                {
                    continue;
                }

                if (!positions.TryGetValue(employment.positionInstanceId ?? string.Empty, out PositionInstanceData position)
                    || !(position.holderPersonIds ?? Array.Empty<string>()).Contains(employment.personId, StringComparer.Ordinal))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "EmploymentHolderMissingFromPosition", "Active employment holder must be listed by the owning position.", employment.employmentId, "Employment.personId -> Position.holderPersonIds");
                }
            }

            foreach (PositionInstanceData position in positions.Values)
            {
                foreach (string holderId in position.holderPersonIds ?? Array.Empty<string>())
                {
                    if (!employments.Values.Any(item => ActiveEmployment(item.state) && Same(item.personId, holderId) && Same(item.positionInstanceId, position.positionInstanceId)))
                    {
                        report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "PositionHolderMissingEmployment", "Position holder must have matching active employment.", position.positionInstanceId, "Position.holderPersonIds -> Employment");
                    }
                }
            }

            foreach (DutyAssignmentData duty in duties.Values.Where(item => item.state == DutyAssignmentState.Assigned || item.state == DutyAssignmentState.Active))
            {
                if (!employments.TryGetValue(duty.employmentId ?? string.Empty, out EmploymentRecordData employment)
                    || !Same(employment.personId, duty.assignedPersonId)
                    || !ActiveEmployment(employment.state))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "DutyAssignedToNonHolder", "Active duty assignments must target the active employment holder.", duty.assignmentId, "Duty.assignedPersonId -> Employment.personId");
                }
            }
        }

        private static void ValidateReportingCycles(Dictionary<string, PositionInstanceData> positions, Step10IntegrationValidationReport report)
        {
            foreach (PositionInstanceData position in positions.Values)
            {
                HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
                string current = position.positionInstanceId;
                while (positions.TryGetValue(current ?? string.Empty, out PositionInstanceData node) && !string.IsNullOrWhiteSpace(node.supervisorPositionInstanceId))
                {
                    if (!visited.Add(current))
                    {
                        report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "ReportingCycle", "Position reporting relationships cannot contain cycles.", position.positionInstanceId, "Position.supervisorPositionInstanceId");
                        break;
                    }

                    current = node.supervisorPositionInstanceId;
                }
            }
        }

        private static void ValidateCareerHistory(CareerHistoryRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, HashSet<string> organizations, Dictionary<string, PersonProfessionRelationshipData> professions, Dictionary<string, TrainingEnrollmentData> training, Dictionary<string, ProfessionalActivityRecordData> activities, Dictionary<string, ProfessionalExperienceEvidenceData> evidence, Dictionary<string, CredentialRecordData> credentials, Dictionary<string, ProfessionalRankRecordData> ranks, Dictionary<string, PositionInstanceData> positions, Dictionary<string, EmploymentRecordData> employments, Step10IntegrationValidationReport report)
        {
            Dictionary<string, CareerEpisodeData> episodes = Index(save.episodes, item => item.episodeId, "CareerEpisode", report);
            Dictionary<string, CareerTransitionRecordData> transitions = Index(save.transitions, item => item.transitionId, "CareerTransition", report);
            Index(save.milestones, item => item.milestoneId, "CareerMilestone", report);

            foreach (CareerEpisodeData episode in save.episodes ?? new List<CareerEpisodeData>())
            {
                if (episode == null)
                {
                    continue;
                }

                ReferencePerson(persons, episode.personId, episode.episodeId, "UnknownCareerPerson", report, "CareerEpisode.personId");
                ReferenceDefinition<ProfessionDefinition>(registry, episode.professionId, episode.episodeId, "MissingCareerProfessionDefinition", report, "CareerEpisode.professionId", allowEmpty: true);
                ReferenceOrganization(organizations, episode.organizationId, episode.episodeId, "UnknownCareerOrganization", report, "CareerEpisode.organizationId", allowEmpty: true);
                ReferenceRecord(employments, episode.employmentId, episode.episodeId, "CareerEmploymentMissing", report, "CareerEpisode.employmentId -> Employment.employmentId", allowEmpty: true);
                ReferenceRecord(ranks, episode.rankRecordId, episode.episodeId, "CareerRankMissing", report, "CareerEpisode.rankRecordId -> Rank.rankRecordId", allowEmpty: true);
                ReferenceRecord(credentials, episode.credentialId, episode.episodeId, "CareerCredentialMissing", report, "CareerEpisode.credentialId -> Credential.credentialId", allowEmpty: true);

                if (episode.state == CareerEpisodeState.Active
                    && !string.IsNullOrWhiteSpace(episode.employmentId)
                    && employments.TryGetValue(episode.employmentId, out EmploymentRecordData employment)
                    && !ActiveEmployment(employment.state))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.Lifecycle, "ActiveEpisodeAfterEmploymentEnded", "Active career episodes cannot depend on ended employment.", episode.episodeId, "CareerEpisode.employmentId -> Employment.state");
                }
            }

            foreach (CareerTransitionRecordData transition in save.transitions ?? new List<CareerTransitionRecordData>())
            {
                if (transition == null)
                {
                    continue;
                }

                ReferencePerson(persons, transition.personId, transition.transitionId, "UnknownTransitionPerson", report, "CareerTransition.personId");
                ReferenceDefinition<CareerTransitionDefinition>(registry, transition.transitionDefinitionId, transition.transitionId, "MissingCareerTransitionDefinition", report, "CareerTransition.transitionDefinitionId");
                foreach (string id in transition.sourceEpisodeIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(episodes, id, transition.transitionId, "TransitionSourceEpisodeMissing", report, "CareerTransition.sourceEpisodeIds -> CareerEpisode.episodeId", allowEmpty: true);
                }

                foreach (string id in transition.destinationEpisodeIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(episodes, id, transition.transitionId, "TransitionDestinationEpisodeMissing", report, "CareerTransition.destinationEpisodeIds -> CareerEpisode.episodeId", allowEmpty: true);
                }
            }
        }

        private static void ValidateLifePaths(LifePathRuntimeSaveData save, DefinitionRegistry registry, HashSet<string> persons, HashSet<string> organizations, Dictionary<string, PersonProfessionRelationshipData> professions, Dictionary<string, TrainingEnrollmentData> training, Dictionary<string, ProfessionalActivityRecordData> activities, Dictionary<string, CredentialRecordData> credentials, Dictionary<string, ProfessionalRankRecordData> ranks, Dictionary<string, PositionInstanceData> positions, Dictionary<string, EmploymentRecordData> employments, CareerHistoryRuntimeSaveData careerSave, Step10IntegrationValidationReport report)
        {
            Dictionary<string, CareerEpisodeData> episodes = Index(careerSave.episodes, item => item.episodeId, "CareerEpisode", report, addDuplicateErrors: false);
            Dictionary<string, CareerTransitionRecordData> transitions = Index(careerSave.transitions, item => item.transitionId, "CareerTransition", report, addDuplicateErrors: false);
            Dictionary<string, LifePathRecordData> lifePaths = Index(save.lifePaths, item => item.lifePathId, "LifePath", report);
            Dictionary<string, PersonAspirationData> aspirations = Index(save.aspirations, item => item.aspirationId, "Aspiration", report);
            Dictionary<string, PersonGoalData> goals = Index(save.goals, item => item.goalId, "LifeGoal", report);
            Dictionary<string, ProfessionalIdentityData> identities = Index(save.identities, item => item.identityId, "ProfessionalIdentity", report);
            Index(save.conflicts, item => item.conflictId, "IdentityConflict", report);
            Index(save.achievementSetbacks, item => item.recordId, "LifePathAchievementSetback", report);

            foreach (LifePathRecordData path in save.lifePaths ?? new List<LifePathRecordData>())
            {
                if (path == null)
                {
                    continue;
                }

                ReferencePerson(persons, path.personId, path.lifePathId, "UnknownLifePathPerson", report, "LifePath.personId");
                foreach (string relationshipId in path.professionRelationshipIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(professions, relationshipId, path.lifePathId, "LifePathProfessionRelationshipMissing", report, "LifePath.professionRelationshipIds -> ProfessionRelationship.relationshipId", allowEmpty: true);
                }

                foreach (string aspirationId in path.activeAspirationIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(aspirations, aspirationId, path.lifePathId, "LifePathAspirationMissing", report, "LifePath.activeAspirationIds -> Aspiration.aspirationId", allowEmpty: true);
                }

                foreach (string goalId in path.activeGoalIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(goals, goalId, path.lifePathId, "LifePathGoalMissing", report, "LifePath.activeGoalIds -> Goal.goalId", allowEmpty: true);
                }

                ReferenceRecord(identities, path.primaryProfessionalIdentityId, path.lifePathId, "LifePathPrimaryIdentityMissing", report, "LifePath.primaryProfessionalIdentityId -> ProfessionalIdentity.identityId", allowEmpty: true);
            }

            foreach (PersonAspirationData aspiration in save.aspirations ?? new List<PersonAspirationData>())
            {
                if (aspiration == null)
                {
                    continue;
                }

                ReferencePerson(persons, aspiration.personId, aspiration.aspirationId, "UnknownAspirationPerson", report, "Aspiration.personId");
                ReferenceDefinition<AspirationDefinition>(registry, aspiration.aspirationDefinitionId, aspiration.aspirationId, "MissingAspirationDefinition", report, "Aspiration.aspirationDefinitionId");
                ReferenceDefinition<ProfessionDefinition>(registry, aspiration.targetProfessionId, aspiration.aspirationId, "AspirationTargetProfessionMissing", report, "Aspiration.targetProfessionId", allowEmpty: true);
                foreach (string goalId in aspiration.relatedGoalIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(goals, goalId, aspiration.aspirationId, "AspirationGoalMissing", report, "Aspiration.relatedGoalIds -> Goal.goalId", allowEmpty: true);
                }
            }

            foreach (PersonGoalData goal in save.goals ?? new List<PersonGoalData>())
            {
                if (goal == null)
                {
                    continue;
                }

                ReferencePerson(persons, goal.personId, goal.goalId, "UnknownGoalPerson", report, "Goal.personId");
                ReferenceDefinition<LifeGoalDefinition>(registry, goal.goalDefinitionId, goal.goalId, "MissingGoalDefinition", report, "Goal.goalDefinitionId");
                ReferenceRecord(aspirations, goal.parentAspirationId, goal.goalId, "GoalAspirationMissing", report, "Goal.parentAspirationId -> Aspiration.aspirationId", allowEmpty: true);
                foreach (string dependencyId in goal.dependencyGoalIds ?? Array.Empty<string>())
                {
                    ReferenceRecord(goals, dependencyId, goal.goalId, "GoalDependencyMissing", report, "Goal.dependencyGoalIds -> Goal.goalId", allowEmpty: true);
                }

                ValidateGoalTarget(goal, registry, training, activities, credentials, ranks, positions, episodes, transitions, report);
            }

            foreach (ProfessionalIdentityData identity in save.identities ?? new List<ProfessionalIdentityData>())
            {
                if (identity == null)
                {
                    continue;
                }

                ReferencePerson(persons, identity.personId, identity.identityId, "UnknownIdentityPerson", report, "ProfessionalIdentity.personId");
                ReferenceDefinition<ProfessionDefinition>(registry, identity.professionId, identity.identityId, "IdentityProfessionMissing", report, "ProfessionalIdentity.professionId", allowEmpty: true);
                ReferenceRecord(professions, identity.professionRelationshipId, identity.identityId, "IdentityProfessionRelationshipMissing", report, "ProfessionalIdentity.professionRelationshipId -> ProfessionRelationship.relationshipId", allowEmpty: true);
                ReferenceRecord(episodes, identity.careerEpisodeId, identity.identityId, "IdentityCareerEpisodeMissing", report, "ProfessionalIdentity.careerEpisodeId -> CareerEpisode.episodeId", allowEmpty: true);
                if (!string.IsNullOrWhiteSpace(identity.professionRelationshipId)
                    && professions.TryGetValue(identity.professionRelationshipId, out PersonProfessionRelationshipData relationship)
                    && !Same(relationship.personId, identity.personId))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, "IdentityRelationshipPersonMismatch", "Professional identity must reference a profession relationship owned by the same Person.", identity.identityId, "ProfessionalIdentity -> ProfessionRelationship.personId");
                }
            }
        }

        private static void ValidateGoalTarget(PersonGoalData goal, DefinitionRegistry registry, Dictionary<string, TrainingEnrollmentData> training, Dictionary<string, ProfessionalActivityRecordData> activities, Dictionary<string, CredentialRecordData> credentials, Dictionary<string, ProfessionalRankRecordData> ranks, Dictionary<string, PositionInstanceData> positions, Dictionary<string, CareerEpisodeData> episodes, Dictionary<string, CareerTransitionRecordData> transitions, Step10IntegrationValidationReport report)
        {
            ReferenceDefinition<ProfessionDefinition>(registry, goal.targetProfessionId, goal.goalId, "GoalTargetProfessionMissing", report, "Goal.targetProfessionId", allowEmpty: true);
            ReferenceDefinition<TrainingProgramDefinition>(registry, goal.targetTrainingProgramId, goal.goalId, "GoalTargetTrainingMissing", report, "Goal.targetTrainingProgramId", allowEmpty: true);
            ReferenceDefinition<CredentialDefinition>(registry, goal.targetCredentialDefinitionId, goal.goalId, "GoalTargetCredentialMissing", report, "Goal.targetCredentialDefinitionId", allowEmpty: true);
            ReferenceDefinition<ProfessionalRankDefinition>(registry, goal.targetRankDefinitionId, goal.goalId, "GoalTargetRankMissing", report, "Goal.targetRankDefinitionId", allowEmpty: true);
            ReferenceDefinition<PositionDefinition>(registry, goal.targetPositionDefinitionId, goal.goalId, "GoalTargetPositionMissing", report, "Goal.targetPositionDefinitionId", allowEmpty: true);
            ReferenceDefinition<ProfessionalActivityDefinition>(registry, goal.targetActivityDefinitionId, goal.goalId, "GoalTargetActivityMissing", report, "Goal.targetActivityDefinitionId", allowEmpty: true);
            ReferenceRecord(episodes, goal.targetCareerEpisodeId, goal.goalId, "GoalCareerEpisodeMissing", report, "Goal.targetCareerEpisodeId -> CareerEpisode.episodeId", allowEmpty: true);
            ReferenceRecord(transitions, goal.targetCareerTransitionId, goal.goalId, "GoalCareerTransitionMissing", report, "Goal.targetCareerTransitionId -> CareerTransition.transitionId", allowEmpty: true);

            if (goal.state == PersonGoalState.Completed && !GoalAuthoritativeComplete(goal, training, activities, credentials, ranks, positions, episodes, transitions))
            {
                report.AddError(Step10IntegrationDiagnosticDomain.Lifecycle, "CompletedGoalWithoutAuthoritativeCompletion", "Completed goals must still resolve to authoritative source state.", goal.goalId, "Goal -> authoritative target runtime");
            }
        }

        private static bool GoalAuthoritativeComplete(PersonGoalData goal, Dictionary<string, TrainingEnrollmentData> training, Dictionary<string, ProfessionalActivityRecordData> activities, Dictionary<string, CredentialRecordData> credentials, Dictionary<string, ProfessionalRankRecordData> ranks, Dictionary<string, PositionInstanceData> positions, Dictionary<string, CareerEpisodeData> episodes, Dictionary<string, CareerTransitionRecordData> transitions)
        {
            if (!string.IsNullOrWhiteSpace(goal.targetTrainingProgramId)
                && training.Values.Any(item => Same(item.personId, goal.personId) && Same(item.programId, goal.targetTrainingProgramId) && item.state == TrainingEnrollmentState.Completed))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetCredentialDefinitionId)
                && credentials.Values.Any(item => Same(item.recipientPersonId, goal.personId) && Same(item.credentialDefinitionId, goal.targetCredentialDefinitionId) && item.state == CredentialState.Active))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetRankDefinitionId)
                && ranks.Values.Any(item => Same(item.personId, goal.personId) && Same(item.rankDefinitionId, goal.targetRankDefinitionId) && item.state == ProfessionalRankState.Active))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetPositionDefinitionId)
                && positions.Values.Any(item => Same(item.positionDefinitionId, goal.targetPositionDefinitionId) && (item.holderPersonIds ?? Array.Empty<string>()).Contains(goal.personId, StringComparer.Ordinal)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetActivityDefinitionId)
                && activities.Values.Any(item => Same(item.personId, goal.personId) && Same(item.activityDefinitionId, goal.targetActivityDefinitionId) && item.state == ProfessionalActivityState.Validated))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetCareerEpisodeId) && episodes.TryGetValue(goal.targetCareerEpisodeId, out CareerEpisodeData episode) && Same(episode.personId, goal.personId))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetCareerTransitionId) && transitions.TryGetValue(goal.targetCareerTransitionId, out CareerTransitionRecordData transition) && Same(transition.personId, goal.personId))
            {
                return true;
            }

            return false;
        }

        private static void ValidateAccessLeakage(Step10IntegrationRuntimeSnapshot data, Step10IntegrationValidationReport report)
        {
            foreach (PersonProfessionRelationshipData relationship in data.Professions.relationships ?? new List<PersonProfessionRelationshipData>())
            {
                if (relationship != null && IsSecret(relationship.accessPolicyId, relationship.tags) && relationship.state != ProfessionRelationshipState.Secret)
                {
                    report.AddWarning(Step10IntegrationDiagnosticDomain.Access, "SecretProfessionShouldUseSecretStateOrProjection", "Secret profession relationships must only be exposed through access-aware projections.", relationship.relationshipId, "ProfessionRelationship.accessPolicyId");
                }
            }

            foreach (PersonGoalData goal in data.LifePaths.goals ?? new List<PersonGoalData>())
            {
                if (goal != null && goal.Secret && (goal.authoritativeReferences?.Length ?? 0) > 0 && goal.state != PersonGoalState.Completed)
                {
                    report.AddWarning(Step10IntegrationDiagnosticDomain.Access, "PrivateGoalCarriesAuthoritativeReferences", "Private active goals carry authoritative references and must be redacted from ordinary projections.", goal.goalId, "Goal.authoritativeReferences");
                }
            }
        }

        private static void ValidateAuthorityMap(Step10IntegrationValidationReport report)
        {
            foreach (string domain in AuthorityMap.GroupBy(entry => entry.Domain, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                report.AddError(Step10IntegrationDiagnosticDomain.Authority, "DuplicateAuthorityDomain", "Step 10 authority domains must have exactly one owner.", domain);
            }

            foreach (Step10IntegrationAuthorityEntry entry in AuthorityMap)
            {
                if (string.IsNullOrWhiteSpace(entry.Domain) || string.IsNullOrWhiteSpace(entry.Owner))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.Authority, "IncompleteAuthorityEntry", "Step 10 authority entries must declare a domain and owner.", entry.Domain);
                }
            }
        }

        private static void ValidateSaveSchemas(Step10IntegrationRuntimeSnapshot snapshot, Step10IntegrationValidationReport report)
        {
            CheckSchema(report, "PersonProfessionRuntime", snapshot.Professions.schemaVersion, PersonProfessionRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ProfessionEntryRuntime", snapshot.Entries.schemaVersion, ProfessionEntryRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "TrainingRuntime", snapshot.Training.schemaVersion, TrainingRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ProfessionalActivityRuntime", snapshot.Activities.schemaVersion, ProfessionalActivityRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "CredentialRuntime", snapshot.Credentials.schemaVersion, CredentialRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ProfessionalRankRuntime", snapshot.Ranks.schemaVersion, ProfessionalRankRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "PositionEmploymentRuntime", snapshot.Positions.schemaVersion, PositionEmploymentRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "CareerHistoryRuntime", snapshot.CareerHistory.schemaVersion, CareerHistoryRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "LifePathRuntime", snapshot.LifePaths.schemaVersion, LifePathRuntimeSaveData.CurrentSchemaVersion);
        }

        private static void ValidatePersistenceDependencies(Step10IntegrationValidationReport report)
        {
            HashSet<string> owners = PersistenceDependencies.Select(entry => entry.Owner).ToHashSet(StringComparer.Ordinal);
            foreach (Step10IntegrationDependencyEntry entry in PersistenceDependencies)
            {
                foreach (string dependency in entry.DependsOn)
                {
                    if (!owners.Contains(dependency))
                    {
                        report.AddError(Step10IntegrationDiagnosticDomain.Persistence, "MissingPersistenceDependencyOwner", $"Persistence dependency '{dependency}' is not declared as a Step 10 owner.", entry.Owner);
                    }
                }
            }

            foreach (Step10IntegrationDependencyEntry entry in PersistenceDependencies)
            {
                if (HasDependencyCycle(entry.Owner, entry.Owner, new HashSet<string>(StringComparer.Ordinal)))
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.Persistence, "CyclicPersistenceDependency", "Step 10 persistence dependencies must remain acyclic.", entry.Owner);
                }
            }
        }

        private static void ValidateFingerprintDeterminism(Step10IntegrationRuntimeSnapshot snapshot, Step10IntegrationValidationReport report)
        {
            string first = CreateCanonicalFingerprint(snapshot);
            string second = CreateCanonicalFingerprint(snapshot.Clone());
            if (!Same(first, second))
            {
                report.AddError(Step10IntegrationDiagnosticDomain.Determinism, "FingerprintNondeterministic", "Step 10 canonical runtime fingerprint changed for equivalent snapshots.");
            }
        }

        private static bool HasDependencyCycle(string root, string current, HashSet<string> visited)
        {
            if (!visited.Add(current))
            {
                return false;
            }

            Step10IntegrationDependencyEntry entry = PersistenceDependencies.FirstOrDefault(candidate => Same(candidate.Owner, current));
            if (entry == null)
            {
                return false;
            }

            foreach (string dependency in entry.DependsOn)
            {
                if (Same(dependency, root) || HasDependencyCycle(root, dependency, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> idSelector, string label, Step10IntegrationValidationReport report, bool addDuplicateErrors = true)
        {
            Dictionary<string, T> indexed = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                if (value == null)
                {
                    continue;
                }

                string id = idSelector(value) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    if (addDuplicateErrors)
                    {
                        report.AddError(Step10IntegrationDiagnosticDomain.RuntimeIndex, $"Missing{label}Id", $"{label} records must declare stable IDs.");
                    }

                    continue;
                }

                if (!indexed.TryAdd(id, value) && addDuplicateErrors)
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.RuntimeIndex, $"Duplicate{label}Id", $"{label} runtime records contain duplicate stable IDs.", id);
                }
            }

            return indexed;
        }

        private static bool ReferenceRecord<T>(Dictionary<string, T> records, string id, string ownerId, string code, Step10IntegrationValidationReport report, string graphPath, bool allowEmpty = false, bool addError = true)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (!allowEmpty && addError)
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, code, "Cross-runtime record reference is empty.", ownerId, graphPath);
                }

                return false;
            }

            if (records.ContainsKey(id))
            {
                return true;
            }

            if (addError)
            {
                report.AddError(Step10IntegrationDiagnosticDomain.PersonGraph, code, $"Referenced record '{id}' is missing.", ownerId, graphPath);
            }

            return false;
        }

        private static bool ReferenceDefinition<TDefinition>(DefinitionRegistry registry, string id, string ownerId, string code, Step10IntegrationValidationReport report, string graphPath, bool allowEmpty = false, bool addError = true)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (!allowEmpty && addError)
                {
                    report.AddError(Step10IntegrationDiagnosticDomain.DefinitionCatalog, code, "Definition reference is empty.", ownerId, graphPath);
                }

                return false;
            }

            if (registry == null || registry.TryGet(id, out TDefinition _))
            {
                return true;
            }

            if (addError)
            {
                report.AddError(Step10IntegrationDiagnosticDomain.DefinitionCatalog, code, $"Definition '{id}' is not registered as {typeof(TDefinition).Name}.", ownerId, graphPath);
            }

            return false;
        }

        private static bool ReferencePerson(HashSet<string> persons, string personId, string ownerId, string code, Step10IntegrationValidationReport report, string graphPath, bool allowEmpty = false)
        {
            return ReferenceExternal(persons, personId, ownerId, code, "Person", Step10IntegrationDiagnosticDomain.PersonGraph, report, graphPath, allowEmpty);
        }

        private static bool ReferenceOrganization(HashSet<string> organizations, string organizationId, string ownerId, string code, Step10IntegrationValidationReport report, string graphPath, bool allowEmpty = false)
        {
            return ReferenceExternal(organizations, organizationId, ownerId, code, "Organization", Step10IntegrationDiagnosticDomain.PersonGraph, report, graphPath, allowEmpty);
        }

        private static bool ReferenceAuthority(HashSet<string> authorities, string authorityId, string ownerId, string code, Step10IntegrationValidationReport report, string graphPath, bool allowEmpty = false)
        {
            return ReferenceExternal(authorities, authorityId, ownerId, code, "Authority", Step10IntegrationDiagnosticDomain.PersonGraph, report, graphPath, allowEmpty);
        }

        private static bool ReferenceExternal(HashSet<string> knownValues, string id, string ownerId, string code, string label, Step10IntegrationDiagnosticDomain domain, Step10IntegrationValidationReport report, string graphPath, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (!allowEmpty)
                {
                    report.AddError(domain, code, $"{label} reference is empty.", ownerId, graphPath);
                }

                return false;
            }

            if (knownValues.Count == 0 || knownValues.Contains(id))
            {
                return true;
            }

            report.AddError(domain, code, $"{label} '{id}' is not registered in the Step 10 graph context.", ownerId, graphPath);
            return false;
        }

        private static void CheckSchema(Step10IntegrationValidationReport report, string owner, int actual, int expected)
        {
            if (actual != expected)
            {
                report.AddError(Step10IntegrationDiagnosticDomain.SaveSchema, "UnsupportedSchemaVersion", $"'{owner}' save schema version {actual} is not supported; expected {expected}.", owner);
            }
        }

        private static void AppendSection<T>(StringBuilder builder, string name, IEnumerable<T> values, Func<T, string> idSelector, Func<T, string> serializer)
        {
            builder.Append(name).Append(':');
            foreach (T value in (values ?? Array.Empty<T>()).Where(value => value != null).OrderBy(value => idSelector(value) ?? string.Empty, StringComparer.Ordinal))
            {
                builder.Append(serializer(value)).Append(';');
            }

            builder.AppendLine();
        }

        private static IEnumerable<string> Sorted(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
        }

        private static HashSet<string> CleanSet(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        private static bool Same(string a, string b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

        private static bool TerminalProfession(ProfessionRelationshipState state)
        {
            return state is ProfessionRelationshipState.Inactive or ProfessionRelationshipState.Suspended or ProfessionRelationshipState.Revoked or ProfessionRelationshipState.Abandoned or ProfessionRelationshipState.Retired or ProfessionRelationshipState.Former;
        }

        private static bool ActiveEmployment(EmploymentState state)
        {
            return state is EmploymentState.Active or EmploymentState.Accepted or EmploymentState.Probationary or EmploymentState.OnLeaveFoundation or EmploymentState.Suspended;
        }

        private static bool IsSecret(string accessPolicyId, IEnumerable<string> tags)
        {
            return Same(accessPolicyId, ProfessionContentIds.AccessSecretId)
                || (tags ?? Array.Empty<string>()).Any(tag => tag.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
