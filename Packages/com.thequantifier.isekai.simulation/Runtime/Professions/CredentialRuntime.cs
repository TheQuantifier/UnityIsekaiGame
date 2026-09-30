using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;

namespace UnityIsekaiGame.Professions
{
    public sealed class CredentialRuntime
    {
        private readonly Dictionary<string, CredentialApplicationData> applicationsById = new Dictionary<string, CredentialApplicationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CredentialExaminationAttemptData> attemptsById = new Dictionary<string, CredentialExaminationAttemptData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CredentialRecordData> credentialsById = new Dictionary<string, CredentialRecordData>(StringComparer.Ordinal);
        private readonly List<CredentialHistoryHookData> historyHooks = new List<CredentialHistoryHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private TrainingRuntime training;
        private ProfessionalActivityRuntime professionalActivities;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownAuthorityIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int ApplicationCount => applicationsById.Count;
        public int ExaminationAttemptCount => attemptsById.Count;
        public int CredentialCount => credentialsById.Count;
        public IReadOnlyList<CredentialHistoryHookData> HistoryHooks => historyHooks.Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CredentialApplicationData> Applications => applicationsById.Values.OrderBy(item => item.applicationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CredentialExaminationAttemptData> ExaminationAttempts => attemptsById.Values.OrderBy(item => item.attemptId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CredentialRecordData> Credentials => credentialsById.Values.OrderBy(item => item.credentialId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, IEnumerable<string> persons = null, IEnumerable<string> authorities = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            training = trainingRuntime;
            professionalActivities = activityRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownAuthorityIds = new HashSet<string>((authorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public bool TryGetCredential(string credentialId, out CredentialRecordData credential)
        {
            if (!string.IsNullOrWhiteSpace(credentialId) && credentialsById.TryGetValue(credentialId, out CredentialRecordData found))
            {
                credential = found.Clone();
                return true;
            }

            credential = null;
            return false;
        }

        public bool TryGetApplication(string applicationId, out CredentialApplicationData application)
        {
            if (!string.IsNullOrWhiteSpace(applicationId) && applicationsById.TryGetValue(applicationId, out CredentialApplicationData found))
            {
                application = found.Clone();
                return true;
            }

            application = null;
            return false;
        }

        public bool TryGetExaminationAttempt(string attemptId, out CredentialExaminationAttemptData attempt)
        {
            if (!string.IsNullOrWhiteSpace(attemptId) && attemptsById.TryGetValue(attemptId, out CredentialExaminationAttemptData found))
            {
                attempt = found.Clone();
                return true;
            }

            attempt = null;
            return false;
        }

        public IReadOnlyList<CredentialRecordData> QueryByRecipient(string personId, bool activeOnly = false)
        {
            return credentialsById.Values
                .Where(item => string.Equals(item.recipientPersonId, personId ?? string.Empty, StringComparison.Ordinal) && (!activeOnly || item.state == CredentialState.Active))
                .OrderBy(item => item.credentialDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.credentialId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public CredentialQualificationResult EvaluateQualification(string personId, string credentialDefinitionId, bool perceived = false, bool privilegedDiagnostics = false)
        {
            List<string> satisfied = new List<string>();
            List<string> blockers = new List<string>();
            List<string> optional = new List<string>();
            List<string> expiring = new List<string>();

            if (!KnownPerson(personId))
            {
                blockers.Add("person.missing");
            }

            if (!TryCredentialDefinition(credentialDefinitionId, out CredentialDefinition definition))
            {
                blockers.Add("credential-definition.missing");
                return BuildQualificationResult(personId, credentialDefinitionId, string.Empty, string.Empty, false, perceived, satisfied, blockers, optional, expiring, privilegedDiagnostics);
            }

            string professionId = definition.RelatedProfessionIds.FirstOrDefault() ?? string.Empty;
            string specializationId = definition.RelatedSpecializationIds.FirstOrDefault() ?? string.Empty;
            if (definition.RequireProfessionRelationship)
            {
                bool hasProfession = professions != null && professions.QueryByProfession(professionId, activeOnly: true).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal));
                if (hasProfession)
                {
                    satisfied.Add($"profession:{professionId}");
                }
                else
                {
                    blockers.Add($"profession:{professionId}");
                }
            }

            if (definition.RequireFormalRecognition)
            {
                bool recognized = professions != null && professions.QueryByProfession(professionId, activeOnly: true).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal) && item.Data.recognized);
                if (recognized)
                {
                    satisfied.Add($"recognition:{professionId}");
                }
                else
                {
                    blockers.Add($"recognition:{professionId}");
                }
            }

            foreach (string programId in definition.RequiredTrainingProgramIds)
            {
                bool completed = training != null && training.QueryByProgram(programId).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal) && item.State == TrainingEnrollmentState.Completed);
                if (completed)
                {
                    satisfied.Add($"training:{programId}");
                }
                else
                {
                    blockers.Add($"training:{programId}");
                }
            }

            ProfessionalExperienceRequirementData requirement = definition.ExperienceRequirement;
            if (requirement != null && requirement.minimumValidatedActivities > 0)
            {
                bool experience = professionalActivities != null && professionalActivities.EvaluateExperienceRequirement(personId, requirement, out _);
                if (experience)
                {
                    satisfied.Add($"experience:{requirement.professionId}");
                }
                else
                {
                    blockers.Add($"experience:{requirement.professionId}");
                }
            }

            foreach (string examinationId in definition.RequiredExaminationDefinitionIds)
            {
                bool passed = attemptsById.Values.Any(item => string.Equals(item.applicantPersonId, personId ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(item.examinationDefinitionId, examinationId, StringComparison.Ordinal)
                    && item.state == CredentialExaminationAttemptState.Passed);
                if (passed)
                {
                    satisfied.Add($"examination:{examinationId}");
                }
                else
                {
                    blockers.Add($"examination:{examinationId}");
                }
            }

            foreach (string recommendationId in definition.RequiredRecommendationIds)
            {
                optional.Add($"recommendation:{recommendationId}");
            }

            bool qualified = blockers.Count == 0;
            bool perceivedQualified = perceived ? satisfied.Count > 0 && !blockers.Any(item => item.IndexOf("credential-definition", StringComparison.Ordinal) >= 0) : qualified;
            return BuildQualificationResult(personId, credentialDefinitionId, professionId, specializationId, qualified, perceivedQualified, satisfied, blockers, optional, expiring, privilegedDiagnostics);
        }

        public CredentialOperationResult SubmitApplication(string applicationId, string personId, string credentialDefinitionId, CredentialIssuerReferenceData issuer, CredentialQualificationSnapshotData qualification, string worldTime, string transactionId, string provenance = "", bool preview = false)
        {
            long before = revision;
            if (!TryCredentialDefinition(credentialDefinitionId, out CredentialDefinition definition))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingDefinition, $"Credential definition '{credentialDefinitionId}' is missing.", revision);
            }

            if (!KnownPerson(personId))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingPerson, $"Person '{personId}' is not known.", revision);
            }

            CredentialIssuerReferenceData effectiveIssuer = issuer?.Clone() ?? new CredentialIssuerReferenceData();
            if (!IssuerAuthorized(definition, effectiveIssuer))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.UnauthorizedIssuer, $"Issuer '{effectiveIssuer.issuerId}' cannot receive or issue '{credentialDefinitionId}'.", revision);
            }

            string id = string.IsNullOrWhiteSpace(applicationId) ? $"credential-application.{personId}.{credentialDefinitionId}" : applicationId.Trim();
            if (applicationsById.ContainsKey(id))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.DuplicateApplication, $"Credential application '{id}' already exists.", revision);
            }

            if (applicationsById.Values.Any(item => string.Equals(item.applicantPersonId, personId, StringComparison.Ordinal)
                && string.Equals(item.credentialDefinitionId, credentialDefinitionId, StringComparison.Ordinal)
                && item.state is CredentialApplicationState.Submitted or CredentialApplicationState.UnderReview or CredentialApplicationState.AwaitingEvidence or CredentialApplicationState.AwaitingExamination))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.DuplicateApplication, "An active application already exists for this credential.", revision);
            }

            CredentialQualificationSnapshotData snapshot = qualification?.Clone() ?? EvaluateQualification(personId, credentialDefinitionId).Snapshot;
            CredentialApplicationData application = new CredentialApplicationData
            {
                applicationId = id,
                applicantPersonId = personId,
                credentialDefinitionId = credentialDefinitionId,
                requestedIssuer = effectiveIssuer,
                relatedProfessionId = definition.RelatedProfessionIds.FirstOrDefault() ?? string.Empty,
                relatedSpecializationId = definition.RelatedSpecializationIds.FirstOrDefault() ?? string.Empty,
                submissionWorldTime = worldTime ?? string.Empty,
                qualificationSnapshot = snapshot,
                supportingTrainingRecordIds = definition.RequiredTrainingProgramIds.ToArray(),
                supportingExperienceEvidenceIds = professionalActivities?.BuildExperienceSummary(personId, definition.ExperienceRequirement.professionId).Evidence.Select(item => item.evidenceId).ToArray() ?? Array.Empty<string>(),
                state = CredentialApplicationState.Submitted,
                accessPolicyId = definition.AccessPolicyId,
                provenance = provenance ?? string.Empty,
                revision = 1L
            };

            if (preview)
            {
                return CredentialOperationResult.Success("Credential application previewed.", before, before, application: application, preview: true);
            }

            applicationsById[id] = application.Clone();
            revision++;
            dirty = true;
            AddHook(CredentialHistoryHookKind.ApplicationSubmitted, applicationId: id, personId: personId, issuerId: effectiveIssuer.issuerId, worldTime: worldTime, transactionId: transactionId);
            return CredentialOperationResult.Success("Credential application submitted.", before, revision, application: application);
        }

        public CredentialOperationResult RequestAdditionalEvidence(string applicationId, string decisionMakerId, string reason, string worldTime, string transactionId, bool preview = false)
        {
            return SetApplicationState(applicationId, CredentialApplicationState.AwaitingEvidence, decisionMakerId, reason, worldTime, transactionId, preview, "Credential application requires additional evidence.");
        }

        public CredentialOperationResult ApproveApplication(string applicationId, string decisionMakerId, CredentialQualificationSnapshotData currentQualification, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(applicationId ?? string.Empty, out CredentialApplicationData application))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingApplication, $"Credential application '{applicationId}' is missing.", revision);
            }

            if (application.state is CredentialApplicationState.Rejected or CredentialApplicationState.Withdrawn or CredentialApplicationState.Cancelled or CredentialApplicationState.Expired or CredentialApplicationState.Invalid)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidState, $"Credential application state '{application.state}' cannot be approved.", revision);
            }

            CredentialQualificationResult current = currentQualification == null
                ? EvaluateQualification(application.applicantPersonId, application.credentialDefinitionId)
                : new CredentialQualificationResult(currentQualification);
            if (!current.AuthoritativeQualified)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingQualification, "Credential qualification is not satisfied.", revision, current);
            }

            if (!application.qualificationSnapshot.SemanticallyEquals(current.Snapshot))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.StaleQualification, "Credential application qualification snapshot is stale.", revision, current);
            }

            CredentialApplicationData updated = application.Clone();
            updated.state = CredentialApplicationState.Approved;
            updated.decisionMakerId = decisionMakerId ?? string.Empty;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.decisionReason = "Approved";
            updated.revision++;
            if (preview)
            {
                return CredentialOperationResult.Success("Credential approval previewed.", before, before, qualification: current, application: updated, preview: true);
            }

            applicationsById[updated.applicationId] = updated.Clone();
            revision++;
            dirty = true;
            return CredentialOperationResult.Success("Credential application approved.", before, revision, qualification: current, application: updated);
        }

        public CredentialOperationResult RejectApplication(string applicationId, string decisionMakerId, string reason, string worldTime, string transactionId, bool preview = false)
        {
            return SetApplicationState(applicationId, CredentialApplicationState.Rejected, decisionMakerId, reason, worldTime, transactionId, preview, "Credential application rejected.");
        }

        public CredentialOperationResult WithdrawApplication(string applicationId, string transactionId, bool preview = false)
        {
            return SetApplicationState(applicationId, CredentialApplicationState.Withdrawn, string.Empty, "Withdrawn", string.Empty, transactionId, preview, "Credential application withdrawn.");
        }

        public CredentialOperationResult RecordExaminationAttempt(CredentialExaminationAttemptData attempt, string transactionId, bool preview = false)
        {
            long before = revision;
            CredentialExaminationAttemptData data = attempt?.Clone();
            if (data == null || string.IsNullOrWhiteSpace(data.attemptId) || string.IsNullOrWhiteSpace(data.examinationDefinitionId) || string.IsNullOrWhiteSpace(data.applicantPersonId))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidRequest, "Examination attempt requires IDs.", revision);
            }

            if (attemptsById.ContainsKey(data.attemptId))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.Duplicate, $"Examination attempt '{data.attemptId}' already exists.", revision);
            }

            if (!KnownPerson(data.applicantPersonId) || (!string.IsNullOrWhiteSpace(data.evaluatorPersonId) && !KnownPerson(data.evaluatorPersonId)))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingPerson, "Examination applicant or evaluator is unknown.", revision);
            }

            if (!registry.TryGet(data.examinationDefinitionId, out CredentialExaminationDefinition definition))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingDefinition, $"Examination definition '{data.examinationDefinitionId}' is missing.", revision);
            }

            if (definition.RequiredEvaluatorAuthorityIds.Count > 0 && !definition.RequiredEvaluatorAuthorityIds.Contains(data.evaluatorAuthorityId ?? string.Empty))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.UnauthorizedEvaluator, "Evaluator authority is not authorized for this examination.", revision);
            }

            int previousAttempts = attemptsById.Values.Count(item => string.Equals(item.applicantPersonId, data.applicantPersonId, StringComparison.Ordinal) && string.Equals(item.examinationDefinitionId, data.examinationDefinitionId, StringComparison.Ordinal));
            if (previousAttempts >= definition.AttemptLimit)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidState, "Examination attempt limit reached.", revision);
            }

            bool passed = data.score >= definition.PassingScore && (data.sectionResults.Length == 0 || data.sectionResults.All(section => section.passed));
            data.state = passed ? CredentialExaminationAttemptState.Passed : data.state == CredentialExaminationAttemptState.Incomplete ? CredentialExaminationAttemptState.Incomplete : CredentialExaminationAttemptState.Failed;
            data.assessmentCategory = data.assessmentCategory == CredentialAssessmentCategory.Custom ? definition.AssessmentCategory : data.assessmentCategory;
            data.accessPolicyId = string.IsNullOrWhiteSpace(data.accessPolicyId) ? definition.AccessPolicyId : data.accessPolicyId;
            if (preview)
            {
                return CredentialOperationResult.Success("Examination attempt previewed.", before, before, attempt: data, preview: true);
            }

            attemptsById[data.attemptId] = data.Clone();
            revision++;
            dirty = true;
            AddHook(passed ? CredentialHistoryHookKind.ExaminationPassed : CredentialHistoryHookKind.ExaminationFailed, examinationAttemptId: data.attemptId, personId: data.applicantPersonId, issuerId: data.evaluatorAuthorityId, worldTime: data.completionWorldTime, transactionId: transactionId);
            return CredentialOperationResult.Success("Examination attempt recorded.", before, revision, attempt: data);
        }

        public CredentialOperationResult IssueCredential(string credentialId, string credentialDefinitionId, string recipientPersonId, CredentialIssuerReferenceData issuer, string applicationId, string examinationAttemptId, string registrationNumber, CredentialQualificationSnapshotData expectedQualification, string issueWorldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!TryCredentialDefinition(credentialDefinitionId, out CredentialDefinition definition))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingDefinition, $"Credential definition '{credentialDefinitionId}' is missing.", revision);
            }

            if (!KnownPerson(recipientPersonId))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingPerson, $"Person '{recipientPersonId}' is unknown.", revision);
            }

            CredentialIssuerReferenceData effectiveIssuer = issuer?.Clone() ?? new CredentialIssuerReferenceData();
            if (!IssuerAuthorized(definition, effectiveIssuer))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.UnauthorizedIssuer, "Issuer is not authorized for this credential.", revision);
            }

            CredentialQualificationResult currentQualification = EvaluateQualification(recipientPersonId, credentialDefinitionId, privilegedDiagnostics: true);
            if (!currentQualification.AuthoritativeQualified)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingQualification, "Credential qualification is not currently satisfied.", revision, currentQualification);
            }

            if (expectedQualification != null && !expectedQualification.SemanticallyEquals(currentQualification.Snapshot))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.StaleQualification, "Credential qualification snapshot is stale.", revision, currentQualification);
            }

            CredentialApplicationData application = null;
            if (definition.RequiresApplication)
            {
                if (!applicationsById.TryGetValue(applicationId ?? string.Empty, out application))
                {
                    return CredentialOperationResult.Failure(CredentialOperationStatus.MissingApplication, "Approved application is required.", revision, currentQualification);
                }

                if (application.state != CredentialApplicationState.Approved || !string.Equals(application.applicantPersonId, recipientPersonId, StringComparison.Ordinal) || !string.Equals(application.credentialDefinitionId, credentialDefinitionId, StringComparison.Ordinal))
                {
                    return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidState, "Application is not approved for this recipient and credential.", revision, currentQualification);
                }
            }

            foreach (string requiredExam in definition.RequiredExaminationDefinitionIds)
            {
                bool passed = attemptsById.Values.Any(item => string.Equals(item.examinationDefinitionId, requiredExam, StringComparison.Ordinal) && string.Equals(item.applicantPersonId, recipientPersonId, StringComparison.Ordinal) && item.state == CredentialExaminationAttemptState.Passed);
                if (!passed)
                {
                    return CredentialOperationResult.Failure(CredentialOperationStatus.MissingExamination, $"Required examination '{requiredExam}' has not been passed.", revision, currentQualification);
                }
            }

            string id = string.IsNullOrWhiteSpace(credentialId) ? $"credential-record.{recipientPersonId}.{credentialDefinitionId}" : credentialId.Trim();
            if (credentialsById.ContainsKey(id))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.Duplicate, $"Credential '{id}' already exists.", revision, currentQualification);
            }

            if (!definition.AllowMultipleActive && credentialsById.Values.Any(item => string.Equals(item.recipientPersonId, recipientPersonId, StringComparison.Ordinal) && string.Equals(item.credentialDefinitionId, credentialDefinitionId, StringComparison.Ordinal) && item.state == CredentialState.Active))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.DuplicateActiveCredential, "Recipient already has an active credential of this type.", revision, currentQualification);
            }

            if (definition.RequiresUniqueRegistrationNumber && !string.IsNullOrWhiteSpace(registrationNumber) && credentialsById.Values.Any(item => string.Equals(item.registrationNumber, registrationNumber, StringComparison.Ordinal)))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.DuplicateRegistrationNumber, "Credential registration number is already in use.", revision, currentQualification);
            }

            CredentialRecordData credential = new CredentialRecordData
            {
                credentialId = id,
                credentialDefinitionId = credentialDefinitionId,
                recipientPersonId = recipientPersonId,
                issuer = effectiveIssuer,
                issueWorldTime = issueWorldTime ?? string.Empty,
                effectiveWorldTime = issueWorldTime ?? string.Empty,
                expirationWorldTime = ExpirationFor(definition, issueWorldTime),
                state = CredentialState.Active,
                relatedProfessionId = definition.RelatedProfessionIds.FirstOrDefault() ?? string.Empty,
                relatedSpecializationId = definition.RelatedSpecializationIds.FirstOrDefault() ?? string.Empty,
                supportingApplicationId = applicationId ?? string.Empty,
                supportingExaminationAttemptId = examinationAttemptId ?? string.Empty,
                supportingTrainingRecordIds = definition.RequiredTrainingProgramIds.ToArray(),
                supportingExperienceEvidenceIds = professionalActivities?.BuildExperienceSummary(recipientPersonId, definition.ExperienceRequirement.professionId).Evidence.Select(item => item.evidenceId).ToArray() ?? Array.Empty<string>(),
                grantedPermissionIds = definition.GrantedPermissionIds.ToArray(),
                authenticityState = CredentialAuthenticityState.Authoritative,
                registrationNumber = registrationNumber ?? string.Empty,
                accessPolicyId = definition.AccessPolicyId,
                provenance = application?.provenance ?? transactionId ?? string.Empty,
                revisionHistory = new[] { $"issued:{issueWorldTime ?? string.Empty}" },
                revision = 1L
            };

            if (preview)
            {
                return CredentialOperationResult.Success("Credential issuance previewed.", before, before, qualification: currentQualification, credential: credential, preview: true);
            }

            credentialsById[id] = credential.Clone();
            revision++;
            dirty = true;
            AddHook(CredentialHistoryHookKind.CredentialIssued, credentialId: id, applicationId: applicationId, examinationAttemptId: examinationAttemptId, personId: recipientPersonId, issuerId: effectiveIssuer.issuerId, worldTime: issueWorldTime, transactionId: transactionId);
            return CredentialOperationResult.Success("Credential issued.", before, revision, qualification: currentQualification, credential: credential);
        }

        public bool HasActivePermission(string personId, string permissionId, CredentialPermissionStatePolicy policy = CredentialPermissionStatePolicy.ActiveOnly)
        {
            return credentialsById.Values.Any(item => string.Equals(item.recipientPersonId, personId ?? string.Empty, StringComparison.Ordinal)
                && item.grantedPermissionIds.Contains(permissionId ?? string.Empty)
                && CredentialSatisfiesPolicy(item.state, policy)
                && item.authenticityState != CredentialAuthenticityState.ForgedClaim);
        }

        public CredentialOperationResult ExpireCredential(string credentialId, string worldTime, string transactionId, bool preview = false)
        {
            return SetCredentialState(credentialId, CredentialState.Expired, CredentialHistoryHookKind.CredentialExpired, "Credential expired.", worldTime, transactionId, preview);
        }

        public CredentialOperationResult RenewCredential(string credentialId, CredentialQualificationSnapshotData currentQualification, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!credentialsById.TryGetValue(credentialId ?? string.Empty, out CredentialRecordData credential))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingCredential, $"Credential '{credentialId}' is missing.", revision);
            }

            if (!TryCredentialDefinition(credential.credentialDefinitionId, out CredentialDefinition definition))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingDefinition, "Credential definition is missing.", revision);
            }

            if (definition.RenewalPolicy == CredentialRenewalPolicy.NotRenewable)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidTransition, "Credential definition is not renewable.", revision);
            }

            CredentialQualificationResult current = currentQualification == null ? EvaluateQualification(credential.recipientPersonId, credential.credentialDefinitionId) : new CredentialQualificationResult(currentQualification);
            if (!current.AuthoritativeQualified)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingQualification, "Credential renewal qualification is not satisfied.", revision, current);
            }

            CredentialRecordData updated = credential.Clone();
            updated.state = CredentialState.Active;
            updated.expirationWorldTime = ExpirationFor(definition, worldTime);
            updated.revision++;
            updated.revisionHistory = AddSorted(updated.revisionHistory, $"renewed:{worldTime ?? string.Empty}");
            if (preview)
            {
                return CredentialOperationResult.Success("Credential renewal previewed.", before, before, qualification: current, credential: updated, preview: true);
            }

            credentialsById[updated.credentialId] = updated.Clone();
            revision++;
            dirty = true;
            AddHook(CredentialHistoryHookKind.CredentialRenewed, credentialId: updated.credentialId, personId: updated.recipientPersonId, issuerId: updated.issuer.issuerId, worldTime: worldTime, transactionId: transactionId);
            return CredentialOperationResult.Success("Credential renewed.", before, revision, qualification: current, credential: updated);
        }

        public CredentialOperationResult SuspendCredential(string credentialId, string worldTime, string transactionId, bool preview = false) => SetCredentialState(credentialId, CredentialState.Suspended, CredentialHistoryHookKind.CredentialSuspended, "Credential suspended.", worldTime, transactionId, preview);
        public CredentialOperationResult ReinstateCredential(string credentialId, string worldTime, string transactionId, bool preview = false) => SetCredentialState(credentialId, CredentialState.Active, CredentialHistoryHookKind.CredentialReinstated, "Credential reinstated.", worldTime, transactionId, preview);
        public CredentialOperationResult RevokeCredential(string credentialId, string worldTime, string transactionId, bool preview = false) => SetCredentialState(credentialId, CredentialState.Revoked, CredentialHistoryHookKind.CredentialRevoked, "Credential revoked.", worldTime, transactionId, preview);
        public CredentialOperationResult SurrenderCredential(string credentialId, string worldTime, string transactionId, bool preview = false) => SetCredentialState(credentialId, CredentialState.Surrendered, CredentialHistoryHookKind.CredentialSurrendered, "Credential surrendered.", worldTime, transactionId, preview);
        public CredentialOperationResult MarkDisputed(string credentialId, string worldTime, string transactionId, bool preview = false) => SetCredentialState(credentialId, CredentialState.Disputed, CredentialHistoryHookKind.CredentialDisputed, "Credential disputed.", worldTime, transactionId, preview);

        public CredentialOperationResult ReplaceCredential(string credentialId, string replacementId, string registrationNumber, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!credentialsById.TryGetValue(credentialId ?? string.Empty, out CredentialRecordData existing))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingCredential, $"Credential '{credentialId}' is missing.", revision);
            }

            if (credentialsById.ContainsKey(replacementId ?? string.Empty))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.Duplicate, "Replacement credential already exists.", revision);
            }

            CredentialRecordData replaced = existing.Clone();
            CredentialRecordData replacement = existing.Clone();
            replaced.state = CredentialState.Replaced;
            replaced.replacedByCredentialId = replacementId ?? string.Empty;
            replaced.revision++;
            replacement.credentialId = replacementId ?? string.Empty;
            replacement.replacesCredentialId = existing.credentialId;
            replacement.registrationNumber = registrationNumber ?? string.Empty;
            replacement.issueWorldTime = worldTime ?? string.Empty;
            replacement.effectiveWorldTime = worldTime ?? string.Empty;
            replacement.state = CredentialState.Active;
            replacement.revision = 1L;
            replacement.revisionHistory = new[] { $"replacement:{existing.credentialId}:{worldTime ?? string.Empty}" };
            if (preview)
            {
                return CredentialOperationResult.Success("Credential replacement previewed.", before, before, credential: replacement, preview: true);
            }

            credentialsById[replaced.credentialId] = replaced.Clone();
            credentialsById[replacement.credentialId] = replacement.Clone();
            revision++;
            dirty = true;
            AddHook(CredentialHistoryHookKind.CredentialReplaced, credentialId: replacement.credentialId, personId: replacement.recipientPersonId, issuerId: replacement.issuer.issuerId, worldTime: worldTime, transactionId: transactionId);
            return CredentialOperationResult.Success("Credential replaced.", before, revision, credential: replacement);
        }

        public CredentialOperationResult RecordForgedClaim(string credentialId, string credentialDefinitionId, string claimantPersonId, string claimedIssuerId, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!TryCredentialDefinition(credentialDefinitionId, out CredentialDefinition definition))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingDefinition, "Credential definition is missing.", revision);
            }

            string id = string.IsNullOrWhiteSpace(credentialId) ? $"credential-forged-claim.{claimantPersonId}.{credentialDefinitionId}" : credentialId.Trim();
            if (credentialsById.ContainsKey(id))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.Duplicate, $"Credential claim '{id}' already exists.", revision);
            }

            CredentialRecordData forged = new CredentialRecordData
            {
                credentialId = id,
                credentialDefinitionId = credentialDefinitionId ?? string.Empty,
                recipientPersonId = claimantPersonId ?? string.Empty,
                issuer = new CredentialIssuerReferenceData { issuerId = claimedIssuerId ?? string.Empty, issuerKind = CredentialIssuerAuthorityKind.Custom },
                issueWorldTime = worldTime ?? string.Empty,
                effectiveWorldTime = string.Empty,
                state = CredentialState.ForgedClaimFoundation,
                authenticityState = CredentialAuthenticityState.ForgedClaim,
                accessPolicyId = definition.AccessPolicyId,
                provenance = transactionId ?? string.Empty,
                revisionHistory = new[] { $"forged-claim:{worldTime ?? string.Empty}" },
                revision = 1L
            };

            if (preview)
            {
                return CredentialOperationResult.Success("Forged credential claim previewed.", before, before, credential: forged, preview: true);
            }

            credentialsById[forged.credentialId] = forged.Clone();
            revision++;
            dirty = true;
            AddHook(CredentialHistoryHookKind.ForgedCredentialExposed, credentialId: forged.credentialId, personId: forged.recipientPersonId, issuerId: claimedIssuerId, worldTime: worldTime, transactionId: transactionId);
            return CredentialOperationResult.Success("Forged credential claim recorded without authoritative validity.", before, revision, credential: forged);
        }

        public bool VerifyRegistrationNumber(string registrationNumber, out CredentialRecordData credential)
        {
            CredentialRecordData found = credentialsById.Values.FirstOrDefault(item => string.Equals(item.registrationNumber, registrationNumber ?? string.Empty, StringComparison.Ordinal));
            credential = found?.Clone();
            return found != null && found.authenticityState != CredentialAuthenticityState.ForgedClaim;
        }

        public CredentialProjection<CredentialRecordData> ProjectCredential(string credentialId, CredentialProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!credentialsById.TryGetValue(credentialId ?? string.Empty, out CredentialRecordData credential))
            {
                return new CredentialProjection<CredentialRecordData>(null, audience, decision, false, true, Array.Empty<string>(), CredentialInformationSubject.ProtectedFields);
            }

            if (decision != null && decision.Denied)
            {
                return new CredentialProjection<CredentialRecordData>(null, audience, decision, false, true, Array.Empty<string>(), CredentialInformationSubject.ProtectedFields);
            }

            bool redacted = decision != null && decision.Decision == InformationAccessDecisionKind.RedactedAccess;
            CredentialRecordData output = credential.Clone();
            if (redacted)
            {
                output.recipientPersonId = string.Empty;
                output.supportingApplicationId = string.Empty;
                output.supportingExaminationAttemptId = string.Empty;
                output.supportingTrainingRecordIds = Array.Empty<string>();
                output.supportingExperienceEvidenceIds = Array.Empty<string>();
                output.registrationNumber = string.Empty;
                output.provenance = string.Empty;
            }

            return new CredentialProjection<CredentialRecordData>(output, audience, decision, redacted, false, decision?.AllowedDetails ?? Array.Empty<string>(), redacted ? CredentialInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public CredentialRuntimeSaveData CreateSaveData()
        {
            return new CredentialRuntimeSaveData
            {
                revision = revision,
                applications = applicationsById.Values.OrderBy(item => item.applicationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                examinationAttempts = attemptsById.Values.OrderBy(item => item.attemptId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                credentials = credentialsById.Values.OrderBy(item => item.credentialId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public CredentialOperationResult RestoreFromSaveData(CredentialRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, IEnumerable<string> persons, IEnumerable<string> authorities, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, persons, authorities, out string failure))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.RestoreFailed, failure, revision);
            }

            CredentialRuntimeSaveData rollback = CreateSaveData();
            long before = revision;
            try
            {
                Configure(definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, persons, authorities);
                applicationsById.Clear();
                attemptsById.Clear();
                credentialsById.Clear();
                foreach (CredentialApplicationData application in saveData.applications ?? new List<CredentialApplicationData>())
                {
                    applicationsById[application.applicationId] = application.Clone();
                }

                foreach (CredentialExaminationAttemptData attempt in saveData.examinationAttempts ?? new List<CredentialExaminationAttemptData>())
                {
                    attemptsById[attempt.attemptId] = attempt.Clone();
                }

                foreach (CredentialRecordData credential in saveData.credentials ?? new List<CredentialRecordData>())
                {
                    credentialsById[credential.credentialId] = credential.Clone();
                }

                revision = Math.Max(0L, saveData.revision);
                dirty = false;
                if (restoring)
                {
                    historyHooks.Clear();
                }

                return CredentialOperationResult.Success("Credential state restored.", before, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, persons, authorities, restoring);
                return CredentialOperationResult.Failure(CredentialOperationStatus.RestoreFailed, $"Credential restore failed: {exception.Message}", before);
            }
        }

        public static bool ValidateSaveData(CredentialRuntimeSaveData saveData, DefinitionRegistry registry, PersonProfessionRuntime professions, TrainingRuntime training, ProfessionalActivityRuntime activities, IEnumerable<string> persons, IEnumerable<string> authorities, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Credential save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CredentialRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported credential schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> knownPersons = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> knownAuthorities = new HashSet<string>((authorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> applicationIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> attemptIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> credentialIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> registrations = new HashSet<string>(StringComparer.Ordinal);

            foreach (CredentialApplicationData application in saveData.applications ?? new List<CredentialApplicationData>())
            {
                if (application == null || string.IsNullOrWhiteSpace(application.applicationId) || !applicationIds.Add(application.applicationId))
                {
                    failure = "Credential save data has a missing or duplicate application ID.";
                    return false;
                }

                if (!knownPersons.Contains(application.applicantPersonId ?? string.Empty))
                {
                    failure = $"Credential application '{application.applicationId}' references unknown applicant '{application.applicantPersonId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(application.credentialDefinitionId, out CredentialDefinition _))
                {
                    failure = $"Credential application '{application.applicationId}' references missing credential definition '{application.credentialDefinitionId}'.";
                    return false;
                }
            }

            foreach (CredentialExaminationAttemptData attempt in saveData.examinationAttempts ?? new List<CredentialExaminationAttemptData>())
            {
                if (attempt == null || string.IsNullOrWhiteSpace(attempt.attemptId) || !attemptIds.Add(attempt.attemptId))
                {
                    failure = "Credential save data has a missing or duplicate examination attempt ID.";
                    return false;
                }

                if (!knownPersons.Contains(attempt.applicantPersonId ?? string.Empty))
                {
                    failure = $"Examination attempt '{attempt.attemptId}' references unknown applicant '{attempt.applicantPersonId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(attempt.examinationDefinitionId, out CredentialExaminationDefinition _))
                {
                    failure = $"Examination attempt '{attempt.attemptId}' references missing examination definition '{attempt.examinationDefinitionId}'.";
                    return false;
                }
            }

            foreach (CredentialRecordData credential in saveData.credentials ?? new List<CredentialRecordData>())
            {
                if (credential == null || string.IsNullOrWhiteSpace(credential.credentialId) || !credentialIds.Add(credential.credentialId))
                {
                    failure = "Credential save data has a missing or duplicate credential ID.";
                    return false;
                }

                if (!knownPersons.Contains(credential.recipientPersonId ?? string.Empty))
                {
                    failure = $"Credential '{credential.credentialId}' references unknown recipient '{credential.recipientPersonId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(credential.credentialDefinitionId, out CredentialDefinition definition))
                {
                    failure = $"Credential '{credential.credentialId}' references missing credential definition '{credential.credentialDefinitionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(credential.issuer?.issuerId) && knownAuthorities.Count > 0 && !knownAuthorities.Contains(credential.issuer.issuerId))
                {
                    failure = $"Credential '{credential.credentialId}' references unknown issuer '{credential.issuer.issuerId}'.";
                    return false;
                }

                if (credential.state == CredentialState.ForgedClaimFoundation && credential.authenticityState != CredentialAuthenticityState.ForgedClaim)
                {
                    failure = $"Credential '{credential.credentialId}' is a forged claim without forged authenticity state.";
                    return false;
                }

                if (credential.state == CredentialState.Active && credential.authenticityState == CredentialAuthenticityState.ForgedClaim)
                {
                    failure = $"Credential '{credential.credentialId}' treats a forged claim as authoritative.";
                    return false;
                }

                if (definition.RequiresApplication && string.IsNullOrWhiteSpace(credential.supportingApplicationId) && credential.authenticityState != CredentialAuthenticityState.ForgedClaim)
                {
                    failure = $"Credential '{credential.credentialId}' is missing its required application reference.";
                    return false;
                }

                if (definition.RequiresApplication && !string.IsNullOrWhiteSpace(credential.supportingApplicationId) && !applicationIds.Contains(credential.supportingApplicationId))
                {
                    failure = $"Credential '{credential.credentialId}' references missing application '{credential.supportingApplicationId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(credential.supportingExaminationAttemptId) && !attemptIds.Contains(credential.supportingExaminationAttemptId))
                {
                    failure = $"Credential '{credential.credentialId}' references missing examination attempt '{credential.supportingExaminationAttemptId}'.";
                    return false;
                }

                if (definition.RequiresUniqueRegistrationNumber && !string.IsNullOrWhiteSpace(credential.registrationNumber) && !registrations.Add(credential.registrationNumber))
                {
                    failure = $"Credential registration number '{credential.registrationNumber}' is duplicated.";
                    return false;
                }
            }

            return true;
        }

        private CredentialQualificationResult BuildQualificationResult(string personId, string credentialDefinitionId, string professionId, string specializationId, bool authoritativeQualified, bool perceivedQualified, List<string> satisfied, List<string> blockers, List<string> optional, List<string> expiring, bool privilegedDiagnostics)
        {
            string hash = string.Join("|", new[] { credentialDefinitionId ?? string.Empty, personId ?? string.Empty }
                .Concat(CredentialQualificationSnapshotData.Clean(satisfied))
                .Concat(CredentialQualificationSnapshotData.Clean(blockers)));
            CredentialQualificationSnapshotData snapshot = new CredentialQualificationSnapshotData
            {
                credentialDefinitionId = credentialDefinitionId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authoritativeQualified = authoritativeQualified,
                perceivedQualified = perceivedQualified,
                satisfiedRequirementIds = CredentialQualificationSnapshotData.Clean(satisfied),
                blockingRequirementIds = CredentialQualificationSnapshotData.Clean(blockers),
                optionalUnmetRequirementIds = CredentialQualificationSnapshotData.Clean(optional),
                expiringRequirementIds = CredentialQualificationSnapshotData.Clean(expiring),
                professionRevision = professions?.Revision ?? 0L,
                trainingRevision = training?.Revision ?? 0L,
                activityRevision = professionalActivities?.Revision ?? 0L,
                credentialRevision = QualificationCredentialRevision(),
                qualificationHash = hash,
                privilegedDiagnostics = privilegedDiagnostics ? string.Join(" | ", blockers) : string.Empty,
                redactedDiagnostics = blockers.Count == 0 ? "Qualified" : "Requirements unmet"
            };
            return new CredentialQualificationResult(snapshot);
        }

        private bool KnownPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        }

        private bool TryCredentialDefinition(string credentialDefinitionId, out CredentialDefinition definition)
        {
            definition = null;
            return registry != null && !string.IsNullOrWhiteSpace(credentialDefinitionId) && registry.TryGet(credentialDefinitionId, out definition);
        }

        private long QualificationCredentialRevision()
        {
            unchecked
            {
                long value = 17L;
                foreach (CredentialExaminationAttemptData attempt in attemptsById.Values.OrderBy(item => item.attemptId, StringComparer.Ordinal))
                {
                    value = value * 31L + attempt.revision;
                    value = value * 31L + (long)attempt.state;
                    value = value * 31L + StableStringHash(attempt.attemptId);
                    value = value * 31L + StableStringHash(attempt.examinationDefinitionId);
                    value = value * 31L + StableStringHash(attempt.applicantPersonId);
                }

                foreach (CredentialRecordData credential in credentialsById.Values.OrderBy(item => item.credentialId, StringComparer.Ordinal))
                {
                    value = value * 31L + credential.revision;
                    value = value * 31L + (long)credential.state;
                    value = value * 31L + (long)credential.authenticityState;
                    value = value * 31L + StableStringHash(credential.credentialId);
                    value = value * 31L + StableStringHash(credential.credentialDefinitionId);
                    value = value * 31L + StableStringHash(credential.recipientPersonId);
                }

                return value & long.MaxValue;
            }
        }

        private static long StableStringHash(string value)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 1099511628211L;
                }

                return hash;
            }
        }

        private bool IssuerAuthorized(CredentialDefinition definition, CredentialIssuerReferenceData issuer)
        {
            if (definition == null || issuer == null)
            {
                return false;
            }

            bool idAllowed = definition.AuthorizedIssuerIds.Count == 0 || definition.AuthorizedIssuerIds.Contains(issuer.issuerId ?? string.Empty);
            bool kindAllowed = definition.IssuerAuthorityKinds.Count == 0 || definition.IssuerAuthorityKinds.Contains(issuer.issuerKind);
            bool exists = knownAuthorityIds.Count == 0 || knownAuthorityIds.Contains(issuer.issuerId ?? string.Empty);
            return idAllowed && kindAllowed && exists;
        }

        private static bool CredentialSatisfiesPolicy(CredentialState state, CredentialPermissionStatePolicy policy)
        {
            return policy switch
            {
                CredentialPermissionStatePolicy.ActiveOnly => state == CredentialState.Active,
                CredentialPermissionStatePolicy.AnyNonRevoked => state != CredentialState.Revoked && state != CredentialState.Invalid && state != CredentialState.ForgedClaimFoundation,
                CredentialPermissionStatePolicy.HistoricalOnly => state != CredentialState.ForgedClaimFoundation,
                _ => state == CredentialState.Active
            };
        }

        private static string ExpirationFor(CredentialDefinition definition, string issueWorldTime)
        {
            if (definition == null || definition.ExpirationPolicy == CredentialExpirationPolicy.NeverExpires || definition.IssueDurationHours <= 0d)
            {
                return string.Empty;
            }

            return $"{issueWorldTime ?? string.Empty}+{definition.IssueDurationHours:R}h";
        }

        private CredentialOperationResult SetApplicationState(string applicationId, CredentialApplicationState state, string decisionMakerId, string reason, string worldTime, string transactionId, bool preview, string message)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(applicationId ?? string.Empty, out CredentialApplicationData application))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingApplication, $"Credential application '{applicationId}' is missing.", revision);
            }

            CredentialApplicationData updated = application.Clone();
            updated.state = state;
            updated.decisionMakerId = decisionMakerId ?? string.Empty;
            updated.decisionReason = reason ?? string.Empty;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            if (preview)
            {
                return CredentialOperationResult.Success($"{message} Preview.", before, before, application: updated, preview: true);
            }

            applicationsById[updated.applicationId] = updated.Clone();
            revision++;
            dirty = true;
            return CredentialOperationResult.Success(message, before, revision, application: updated);
        }

        private CredentialOperationResult SetCredentialState(string credentialId, CredentialState state, CredentialHistoryHookKind hook, string message, string worldTime, string transactionId, bool preview)
        {
            long before = revision;
            if (!credentialsById.TryGetValue(credentialId ?? string.Empty, out CredentialRecordData credential))
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingCredential, $"Credential '{credentialId}' is missing.", revision);
            }

            if (credential.state == CredentialState.Revoked && state == CredentialState.Active)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidTransition, "Revoked credentials cannot be reinstated.", revision);
            }

            CredentialRecordData updated = credential.Clone();
            updated.state = state;
            updated.revision++;
            updated.revisionHistory = AddSorted(updated.revisionHistory, $"{state}:{worldTime ?? string.Empty}");
            if (preview)
            {
                return CredentialOperationResult.Success($"{message} Preview.", before, before, credential: updated, preview: true);
            }

            credentialsById[updated.credentialId] = updated.Clone();
            revision++;
            dirty = true;
            AddHook(hook, credentialId: updated.credentialId, personId: updated.recipientPersonId, issuerId: updated.issuer.issuerId, worldTime: worldTime, transactionId: transactionId);
            return CredentialOperationResult.Success(message, before, revision, credential: updated);
        }

        private void AddHook(CredentialHistoryHookKind kind, string credentialId = "", string applicationId = "", string examinationAttemptId = "", string personId = "", string issuerId = "", string worldTime = "", string transactionId = "")
        {
            historyHooks.Add(new CredentialHistoryHookData
            {
                kind = kind,
                credentialId = credentialId ?? string.Empty,
                applicationId = applicationId ?? string.Empty,
                examinationAttemptId = examinationAttemptId ?? string.Empty,
                personId = personId ?? string.Empty,
                issuerId = issuerId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            });
        }

        private static string[] AddSorted(IEnumerable<string> existing, string value)
        {
            return CredentialQualificationSnapshotData.Clean((existing ?? Array.Empty<string>()).Concat(new[] { value }));
        }
    }
}
