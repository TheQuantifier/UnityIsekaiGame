using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;

namespace UnityIsekaiGame.Professions
{
    public sealed class TrainingRuntime
    {
        private static readonly string[] ProjectionFields =
        {
            "enrollment-id",
            "person-id",
            "program-id",
            "profession-id",
            "specialization-id",
            "institution",
            "instructors",
            "state",
            "progress",
            "provenance"
        };

        private readonly Dictionary<string, TrainingEnrollmentData> enrollmentsById = new Dictionary<string, TrainingEnrollmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TrainingInstructorAssignmentData> instructorAssignmentsById = new Dictionary<string, TrainingInstructorAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TrainingLearningSessionData> sessionsById = new Dictionary<string, TrainingLearningSessionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TrainingPracticalWorkRecordData> practicalWorkById = new Dictionary<string, TrainingPracticalWorkRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TrainingSupervisedWorkRecordData> supervisedWorkById = new Dictionary<string, TrainingSupervisedWorkRecordData>(StringComparer.Ordinal);
        private readonly HashSet<string> exclusiveActivityReferences = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TrainingHistoryHookData> historyHooks = new List<TrainingHistoryHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private InformationTransferRuntime transfers;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int EnrollmentCount => enrollmentsById.Count;
        public IReadOnlyList<TrainingHistoryHookData> HistoryHooks => historyHooks.Select(hook => hook.Clone()).ToArray();
        public IReadOnlyList<TrainingEnrollmentSnapshot> Enrollments => enrollmentsById.Values
            .OrderBy(enrollment => enrollment.personId, StringComparer.Ordinal)
            .ThenBy(enrollment => enrollment.programId, StringComparer.Ordinal)
            .ThenBy(enrollment => enrollment.enrollmentId, StringComparer.Ordinal)
            .Select(enrollment => new TrainingEnrollmentSnapshot(enrollment))
            .ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, InformationTransferRuntime transferRuntime, IEnumerable<string> persons = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            transfers = transferRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }

        public bool TryGetEnrollment(string enrollmentId, out TrainingEnrollmentSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(enrollmentId) && enrollmentsById.TryGetValue(enrollmentId, out TrainingEnrollmentData data))
            {
                snapshot = new TrainingEnrollmentSnapshot(data);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<TrainingEnrollmentSnapshot> QueryByPerson(string personId)
        {
            return enrollmentsById.Values
                .Where(enrollment => string.Equals(enrollment.personId, personId ?? string.Empty, StringComparison.Ordinal))
                .OrderBy(enrollment => enrollment.programId, StringComparer.Ordinal)
                .ThenBy(enrollment => enrollment.enrollmentId, StringComparer.Ordinal)
                .Select(enrollment => new TrainingEnrollmentSnapshot(enrollment))
                .ToArray();
        }

        public IReadOnlyList<TrainingEnrollmentSnapshot> QueryByProgram(string programId)
        {
            return enrollmentsById.Values
                .Where(enrollment => string.Equals(enrollment.programId, programId ?? string.Empty, StringComparison.Ordinal))
                .OrderBy(enrollment => enrollment.personId, StringComparer.Ordinal)
                .ThenBy(enrollment => enrollment.enrollmentId, StringComparer.Ordinal)
                .Select(enrollment => new TrainingEnrollmentSnapshot(enrollment))
                .ToArray();
        }

        public TrainingOperationResult ApplyToProgram(string enrollmentId, string personId, string programId, string transactionId, double worldTime = 0d, bool preview = false, string institutionId = "", string provenanceId = "")
        {
            long before = revision;
            if (!ValidateConfigured(out string failure))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingRuntime, failure, before);
            }

            if (!KnownPerson(personId))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingPerson, $"Person '{personId}' is not known.", before);
            }

            if (!ResolveProgram(programId, out TrainingProgramDefinition program, out _))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingDefinition, $"Training program '{programId}' is missing.", before);
            }

            string id = string.IsNullOrWhiteSpace(enrollmentId) ? $"training-enrollment.{personId}.{programId}" : enrollmentId.Trim();
            if (enrollmentsById.TryGetValue(id, out TrainingEnrollmentData existing))
            {
                return TrainingOperationResult.Success("Training enrollment already exists.", before, before, new TrainingEnrollmentSnapshot(existing), duplicate: true);
            }

            if (enrollmentsById.Values.Any(enrollment => IsActive(enrollment.state) && string.Equals(enrollment.personId, personId, StringComparison.Ordinal) && string.Equals(enrollment.programId, programId, StringComparison.Ordinal)))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.Duplicate, $"Person '{personId}' already has an active enrollment for '{programId}'.", before);
            }

            TrainingEnrollmentData data = new TrainingEnrollmentData
            {
                enrollmentId = id,
                personId = personId,
                programId = programId,
                relatedProfessionId = program.RelatedProfessionIds.FirstOrDefault() ?? string.Empty,
                relatedSpecializationId = program.RelatedSpecializationIds.FirstOrDefault() ?? string.Empty,
                institutionOrOrganizationId = string.IsNullOrWhiteSpace(institutionId) ? program.RequiredOrganizationId : institutionId,
                startWorldTime = worldTime.ToString("R"),
                expectedCompletionTime = (worldTime + program.DurationFoundationHours).ToString("R"),
                state = TrainingEnrollmentState.Applied,
                accessPolicyId = program.DefaultAccessPolicyId,
                provenanceId = provenanceId ?? transactionId ?? string.Empty,
                revision = 1L
            };

            if (preview)
            {
                return TrainingOperationResult.Success("Training application previewed.", before, before, new TrainingEnrollmentSnapshot(data), preview: true);
            }

            enrollmentsById.Add(id, data);
            revision++;
            dirty = true;
            AddHook(TrainingHistoryHookKind.ProgramEntered, data, transactionId, worldTime);
            return TrainingOperationResult.Success("Training application recorded.", before, revision, new TrainingEnrollmentSnapshot(data));
        }

        public TrainingOperationResult AcceptEnrollment(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Accepted, transactionId, preview, TrainingHistoryHookKind.ProgramEntered, "Training enrollment accepted.");
        }

        public TrainingOperationResult BeginProgram(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Active, transactionId, preview, TrainingHistoryHookKind.ProgramEntered, "Training program started.");
        }

        public TrainingOperationResult PauseProgram(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Paused, transactionId, preview, TrainingHistoryHookKind.ProgramPaused, "Training program paused.");
        }

        public TrainingOperationResult ResumeProgram(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Active, transactionId, preview, TrainingHistoryHookKind.ProgramEntered, "Training program resumed.");
        }

        public TrainingOperationResult Withdraw(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Withdrawn, transactionId, preview, TrainingHistoryHookKind.Corrected, "Training enrollment withdrawn.");
        }

        public TrainingOperationResult Dismiss(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Dismissed, transactionId, preview, TrainingHistoryHookKind.LearnerDismissed, "Training learner dismissed.");
        }

        public TrainingOperationResult FailProgram(string enrollmentId, string transactionId, bool preview = false)
        {
            return Transition(enrollmentId, TrainingEnrollmentState.Failed, transactionId, preview, TrainingHistoryHookKind.ProgramFailed, "Training program failed.");
        }

        public TrainingOperationResult AssignInstructor(string enrollmentId, string assignmentId, TrainingInstructorRoleKind role, string instructorPersonId, string transactionId, double worldTime = 0d, string professionId = "", string specializationId = "", string authorityId = "", bool preview = false)
        {
            long before = revision;
            if (!TryResolveEnrollment(enrollmentId, out TrainingEnrollmentData enrollment, out TrainingProgramDefinition program, out _))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before);
            }

            if (!KnownPerson(instructorPersonId))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidInstructor, $"Instructor '{instructorPersonId}' is not known.", before);
            }

            string id = string.IsNullOrWhiteSpace(assignmentId) ? $"training-instructor.{enrollmentId}.{role}.{instructorPersonId}" : assignmentId.Trim();
            if (instructorAssignmentsById.TryGetValue(id, out TrainingInstructorAssignmentData existing))
            {
                return TrainingOperationResult.Success("Instructor assignment already exists.", before, before, new TrainingEnrollmentSnapshot(enrollment), duplicate: true);
            }

            TrainingInstructorRequirementData requirement = program.InstructorRequirements.FirstOrDefault(req => req.role == role);
            if (requirement != null && !InstructorMeetsRequirement(instructorPersonId, requirement, professionId, specializationId, authorityId, out string instructorFailure))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidInstructor, instructorFailure, before);
            }

            TrainingInstructorAssignmentData assignment = new TrainingInstructorAssignmentData
            {
                assignmentId = id,
                enrollmentId = enrollmentId,
                role = role,
                personId = instructorPersonId,
                assignedWorldTime = worldTime.ToString("R"),
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                maximumLearnerCapacity = requirement?.maximumLearnerCapacity ?? 1,
                accessPolicyId = requirement?.accessPolicyId ?? program.DefaultAccessPolicyId,
                provenanceId = transactionId ?? string.Empty,
                revision = 1L
            };

            TrainingEnrollmentData working = enrollment.Clone();
            working.instructorAssignmentIds = AddSorted(working.instructorAssignmentIds, id);
            if (program.Category == TrainingProgramCategory.Apprenticeship && role == TrainingInstructorRoleKind.Master)
            {
                working.masterPersonId = instructorPersonId;
            }

            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success("Instructor assignment previewed.", before, before, new TrainingEnrollmentSnapshot(working), preview: true);
            }

            instructorAssignmentsById[id] = assignment;
            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            AddHook(role == TrainingInstructorRoleKind.Master ? TrainingHistoryHookKind.ApprenticeshipBegun : TrainingHistoryHookKind.InstructorAssigned, working, transactionId, worldTime, id);
            return TrainingOperationResult.Success("Instructor assignment recorded.", before, revision, new TrainingEnrollmentSnapshot(working));
        }

        public TrainingOperationResult RunLearningSession(string sessionId, string enrollmentId, string moduleId, string lessonId, string transactionId, InformationTransferRequest transferRequest = null, double startWorldTime = 0d, double completionWorldTime = 0d, bool preview = false)
        {
            long before = revision;
            if (!TryResolveEnrollment(enrollmentId, out TrainingEnrollmentData enrollment, out TrainingProgramDefinition program, out TrainingCurriculumDefinition curriculum))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before);
            }

            if (!CanPerformLearning(enrollment.state))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidState, $"Enrollment '{enrollmentId}' is not active for learning.", before);
            }

            if (!curriculum.TryGetModule(moduleId, out TrainingModuleDefinitionData module))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidModule, $"Module '{moduleId}' is not part of curriculum '{curriculum.Id}'.", before);
            }

            if (!curriculum.TryGetLesson(lessonId, out TrainingLessonDefinitionData lesson) || !string.Equals(lesson.moduleId, moduleId, StringComparison.Ordinal))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidLesson, $"Lesson '{lessonId}' is not part of module '{moduleId}'.", before);
            }

            string id = string.IsNullOrWhiteSpace(sessionId) ? $"training-session.{enrollmentId}.{lessonId}.{transactionId}" : sessionId.Trim();
            if (sessionsById.TryGetValue(id, out TrainingLearningSessionData existing))
            {
                return TrainingOperationResult.Success("Learning session already exists.", before, before, new TrainingEnrollmentSnapshot(enrollment), duplicate: true);
            }

            InformationTransferResult transfer = null;
            if (transferRequest != null)
            {
                if (transfers == null)
                {
                    return TrainingOperationResult.Failure(TrainingOperationStatus.MissingRuntime, "Information transfer runtime is missing.", before);
                }

                transferRequest.TransactionId = string.IsNullOrWhiteSpace(transferRequest.TransactionId) ? transactionId : transferRequest.TransactionId;
                transferRequest.TransferId = string.IsNullOrWhiteSpace(transferRequest.TransferId) ? $"transfer.{id}" : transferRequest.TransferId;
                transferRequest.TeachingRequested = true;
                transferRequest.Mode = MapTransferMode(lesson.teachingMethod);
                transferRequest.TransferDefinitionId = string.IsNullOrWhiteSpace(transferRequest.TransferDefinitionId) ? lesson.informationTransferDefinitionId : transferRequest.TransferDefinitionId;
                transfer = preview ? transfers.PreviewTransfer(transferRequest) : transfers.ExecuteTransfer(transferRequest);
                if (!transfer.Succeeded)
                {
                    return TrainingOperationResult.Failure(TrainingOperationStatus.TeachingFailed, transfer.Message, before, transfer: transfer);
                }
            }

            string[] instructorIds = InstructorPersons(enrollment).ToArray();
            TrainingLearningSessionData session = new TrainingLearningSessionData
            {
                sessionId = id,
                enrollmentId = enrollmentId,
                programId = program.Id,
                moduleId = moduleId,
                lessonId = lessonId,
                instructorIds = instructorIds,
                learnerIds = new[] { enrollment.personId },
                teachingMethod = lesson.teachingMethod,
                sourceOrRecordIds = lesson.sourceOrRecordIds,
                startWorldTime = startWorldTime.ToString("R"),
                completionWorldTime = completionWorldTime.ToString("R"),
                attended = true,
                state = transfer == null || transfer.Succeeded ? TrainingSessionCompletionState.Completed : TrainingSessionCompletionState.Partial,
                transferId = transfer?.Record?.TransferId ?? string.Empty,
                evidenceIds = transfer?.RecipientResults.SelectMany(result => result.CreatedEvidenceIds).ToArray() ?? Array.Empty<string>(),
                accessPolicyId = lesson.accessPolicyId,
                provenanceId = transactionId ?? string.Empty,
                revision = 1L
            };

            TrainingEnrollmentData working = enrollment.Clone();
            working.lessonSessionIds = AddSorted(working.lessonSessionIds, id);
            working.activeModuleIds = AddSorted(working.activeModuleIds, moduleId);
            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success("Learning session previewed.", before, before, new TrainingEnrollmentSnapshot(working), transfer: transfer, preview: true);
            }

            sessionsById[id] = session;
            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            return TrainingOperationResult.Success("Learning session recorded.", before, revision, new TrainingEnrollmentSnapshot(working), transfer: transfer);
        }

        public TrainingOperationResult CompleteModule(string enrollmentId, string moduleId, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!TryResolveEnrollment(enrollmentId, out TrainingEnrollmentData enrollment, out _, out TrainingCurriculumDefinition curriculum))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before);
            }

            if (!curriculum.TryGetModule(moduleId, out TrainingModuleDefinitionData module))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidModule, $"Module '{moduleId}' is not part of curriculum '{curriculum.Id}'.", before);
            }

            bool dependenciesCompleted = ModuleDependenciesCompleted(enrollment, module, out string dependencyFailure);
            bool lessonsCompleted = ModuleLessonsCompleted(enrollment, module, out string lessonFailure);
            bool assignmentsCompleted = ModuleAssignmentsCompleted(enrollment, module, out string assignmentFailure);
            if (!dependenciesCompleted || !lessonsCompleted || !assignmentsCompleted)
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.RequirementBlocked, dependencyFailure + lessonFailure + assignmentFailure, before);
            }

            TrainingEnrollmentData working = enrollment.Clone();
            working.completedModuleIds = AddSorted(working.completedModuleIds, moduleId);
            working.activeModuleIds = RemoveSorted(working.activeModuleIds, moduleId);
            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success("Module completion previewed.", before, before, new TrainingEnrollmentSnapshot(working), preview: true);
            }

            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            return TrainingOperationResult.Success("Training module completed.", before, revision, new TrainingEnrollmentSnapshot(working));
        }

        public TrainingOperationResult RecordPracticalAssignment(string recordId, string enrollmentId, string assignmentId, string activityReferenceId, TrainingAssignmentActivityCategory category, string transactionId, int quantity = 1, int quality = 1000, bool successful = true, string supervisorPersonId = "", double worldTime = 0d, bool preview = false)
        {
            long before = revision;
            if (!TryResolveEnrollment(enrollmentId, out TrainingEnrollmentData enrollment, out _, out TrainingCurriculumDefinition curriculum))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before);
            }

            if (!curriculum.TryGetAssignment(assignmentId, out TrainingPracticalAssignmentDefinitionData assignment))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidAssignment, $"Assignment '{assignmentId}' is not part of curriculum '{curriculum.Id}'.", before);
            }

            if (assignment.activityCategory != category)
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidAssignment, $"Assignment '{assignmentId}' expects {assignment.activityCategory}, not {category}.", before);
            }

            if (assignment.exclusiveActivityReference && exclusiveActivityReferences.Contains(activityReferenceId ?? string.Empty))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.DuplicateActivity, $"Activity '{activityReferenceId}' was already counted for an exclusive training assignment.", before);
            }

            if (assignment.supervisionRequired && string.IsNullOrWhiteSpace(supervisorPersonId))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.RequirementBlocked, $"Assignment '{assignmentId}' requires supervision.", before);
            }

            bool accepted = successful && quantity >= assignment.requiredQuantity && quality >= assignment.qualityThreshold;
            string id = string.IsNullOrWhiteSpace(recordId) ? $"training-practical.{enrollmentId}.{assignmentId}.{activityReferenceId}" : recordId.Trim();
            if (practicalWorkById.TryGetValue(id, out _))
            {
                return TrainingOperationResult.Success("Practical assignment record already exists.", before, before, new TrainingEnrollmentSnapshot(enrollment), duplicate: true);
            }

            TrainingPracticalWorkRecordData record = new TrainingPracticalWorkRecordData
            {
                recordId = id,
                enrollmentId = enrollmentId,
                moduleId = assignment.moduleId,
                assignmentId = assignmentId,
                activityCategory = category,
                activityReferenceId = activityReferenceId ?? string.Empty,
                professionId = assignment.requiredProfessionId,
                quantity = quantity,
                quality = quality,
                successful = successful,
                supervisorPersonId = supervisorPersonId ?? string.Empty,
                accepted = accepted,
                worldTime = worldTime.ToString("R"),
                accessPolicyId = assignment.accessPolicyId,
                provenanceId = transactionId ?? string.Empty,
                revision = 1L
            };

            TrainingEnrollmentData working = enrollment.Clone();
            working.practicalWorkRecordIds = AddSorted(working.practicalWorkRecordIds, id);
            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success("Practical assignment previewed.", before, before, new TrainingEnrollmentSnapshot(working), preview: true);
            }

            practicalWorkById[id] = record;
            if (assignment.exclusiveActivityReference && !string.IsNullOrWhiteSpace(activityReferenceId))
            {
                exclusiveActivityReferences.Add(activityReferenceId);
            }

            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            if (accepted)
            {
                AddHook(TrainingHistoryHookKind.MajorAssignmentCompleted, working, transactionId, worldTime, id);
            }

            return TrainingOperationResult.Success(accepted ? "Practical assignment accepted." : "Practical assignment recorded but not accepted.", before, revision, new TrainingEnrollmentSnapshot(working));
        }

        public TrainingOperationResult RecordSupervisedWork(string recordId, string enrollmentId, string supervisorPersonId, string professionId, string activityReferenceId, TrainingSupervisionLevel supervisionLevel, TrainingWorkOutcome outcome, string transactionId, int quality = 1000, double startWorldTime = 0d, double completionWorldTime = 0d, bool preview = false)
        {
            long before = revision;
            if (!TryResolveEnrollment(enrollmentId, out TrainingEnrollmentData enrollment, out _, out _))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before);
            }

            if (!KnownPerson(supervisorPersonId))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidInstructor, $"Supervisor '{supervisorPersonId}' is not known.", before);
            }

            if (!Enum.IsDefined(typeof(TrainingSupervisionLevel), supervisionLevel) || !Enum.IsDefined(typeof(TrainingWorkOutcome), outcome))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidRequest, "Supervised work has invalid enum data.", before);
            }

            string id = string.IsNullOrWhiteSpace(recordId) ? $"training-supervised.{enrollmentId}.{activityReferenceId}" : recordId.Trim();
            if (supervisedWorkById.ContainsKey(id))
            {
                return TrainingOperationResult.Success("Supervised work record already exists.", before, before, new TrainingEnrollmentSnapshot(enrollment), duplicate: true);
            }

            TrainingSupervisedWorkRecordData record = new TrainingSupervisedWorkRecordData
            {
                recordId = id,
                enrollmentId = enrollmentId,
                learnerPersonId = enrollment.personId,
                supervisorPersonId = supervisorPersonId,
                professionId = professionId ?? enrollment.relatedProfessionId,
                activityReferenceId = activityReferenceId ?? string.Empty,
                startWorldTime = startWorldTime.ToString("R"),
                completionWorldTime = completionWorldTime.ToString("R"),
                supervisionLevel = supervisionLevel,
                learnerResponsibility = "training.practice",
                supervisorParticipation = supervisionLevel.ToString(),
                outcome = outcome,
                quality = quality,
                evaluationSummary = outcome.ToString(),
                provenanceId = transactionId ?? string.Empty,
                revision = 1L
            };

            TrainingEnrollmentData working = enrollment.Clone();
            working.supervisedWorkRecordIds = AddSorted(working.supervisedWorkRecordIds, id);
            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success("Supervised work previewed.", before, before, new TrainingEnrollmentSnapshot(working), preview: true);
            }

            supervisedWorkById[id] = record;
            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            return TrainingOperationResult.Success("Supervised work recorded.", before, revision, new TrainingEnrollmentSnapshot(working));
        }

        public TrainingProgressResult EvaluateProgress(string enrollmentId, bool perceived = false)
        {
            if (!TryResolveEnrollment(enrollmentId, out TrainingEnrollmentData enrollment, out _, out TrainingCurriculumDefinition curriculum))
            {
                return new TrainingProgressResult(enrollmentId, 0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new[] { "EnrollmentMissing" }, false, perceived, CreateProgressToken(enrollmentId, perceived), revision, "Enrollment is missing.");
            }

            List<string> required = curriculum.Modules
                .Where(module => module.required && (!perceived || !module.hiddenFromLearner))
                .Select(module => module.moduleId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            List<string> completed = required.Where(id => enrollment.completedModuleIds.Contains(id, StringComparer.Ordinal)).ToList();
            List<string> remaining = required.Where(id => !completed.Contains(id, StringComparer.Ordinal)).ToList();
            List<string> failed = (enrollment.failedModuleIds ?? Array.Empty<string>()).Where(id => required.Contains(id, StringComparer.Ordinal)).OrderBy(id => id, StringComparer.Ordinal).ToList();
            List<string> blockers = new List<string>();
            if (!CanComplete(enrollment.state))
            {
                blockers.Add($"state:{enrollment.state}");
            }

            blockers.AddRange(failed.Select(id => $"failed:{id}"));
            int percentage = required.Count == 0 ? 100 : (int)Math.Round((double)completed.Count / required.Count * 100d);
            bool eligible = required.Count > 0 && remaining.Count == 0 && failed.Count == 0 && blockers.Count == 0;
            return new TrainingProgressResult(enrollmentId, percentage, completed, remaining, failed, blockers, eligible, perceived, CreateProgressToken(enrollmentId, perceived), revision, perceived ? "Perceived progress hides restricted requirements." : "Authoritative training progress evaluated.");
        }

        public TrainingOperationResult CompleteProgram(string enrollmentId, string transactionId, TrainingProgressTokenData expectedProgressToken, double worldTime = 0d, bool preview = false)
        {
            long before = revision;
            TrainingProgressResult progress = EvaluateProgress(enrollmentId, perceived: false);
            if (expectedProgressToken != null && !expectedProgressToken.SemanticallyEquals(progress.RuntimeToken))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.StaleProgress, "Training completion used a stale progress result.", before, progress);
            }

            if (!progress.EligibleForCompletion)
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.RequirementBlocked, "Training completion requirements are not satisfied.", before, progress);
            }

            if (!enrollmentsById.TryGetValue(enrollmentId, out TrainingEnrollmentData enrollment))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before, progress);
            }

            TrainingEnrollmentData working = enrollment.Clone();
            working.state = TrainingEnrollmentState.Completed;
            working.completionWorldTime = worldTime.ToString("R");
            working.progressSummary = $"{progress.Percentage}% complete";
            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success("Training program completion previewed.", before, before, new TrainingEnrollmentSnapshot(working), progress, preview: true);
            }

            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            AddHook(TrainingHistoryHookKind.ProgramCompleted, working, transactionId, worldTime);
            if (ResolveProgram(working.programId, out TrainingProgramDefinition program, out _) && program.Category == TrainingProgramCategory.Apprenticeship)
            {
                AddHook(TrainingHistoryHookKind.ApprenticeshipCompleted, working, transactionId, worldTime);
            }

            return TrainingOperationResult.Success("Training program completed without automatic credential or profession grants.", before, revision, new TrainingEnrollmentSnapshot(working), progress);
        }

        public TrainingProjection<TrainingEnrollmentSnapshot> ProjectEnrollment(string enrollmentId, TrainingProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!TryGetEnrollment(enrollmentId, out TrainingEnrollmentSnapshot snapshot))
            {
                return new TrainingProjection<TrainingEnrollmentSnapshot>(null, audience, decision, false, true, Array.Empty<string>(), ProjectionFields);
            }

            if (decision != null && decision.Denied)
            {
                return new TrainingProjection<TrainingEnrollmentSnapshot>(null, audience, decision, false, true, Array.Empty<string>(), ProjectionFields);
            }

            bool redacted = decision != null && !decision.FullAccess;
            TrainingEnrollmentSnapshot projected = redacted ? new TrainingEnrollmentSnapshot(Redacted(snapshot.Data)) : snapshot;
            return new TrainingProjection<TrainingEnrollmentSnapshot>(projected, audience, decision, redacted, false, decision?.AllowedDetails ?? ProjectionFields, decision == null ? Array.Empty<string>() : decision.RedactedDetails.Concat(decision.HiddenDetails).ToArray());
        }

        public TrainingRuntimeSaveData CreateSaveData()
        {
            return new TrainingRuntimeSaveData
            {
                schemaVersion = TrainingRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                enrollments = enrollmentsById.Values.OrderBy(data => data.enrollmentId, StringComparer.Ordinal).Select(data => data.Clone()).ToList(),
                instructorAssignments = instructorAssignmentsById.Values.OrderBy(data => data.assignmentId, StringComparer.Ordinal).Select(data => data.Clone()).ToList(),
                learningSessions = sessionsById.Values.OrderBy(data => data.sessionId, StringComparer.Ordinal).Select(data => data.Clone()).ToList(),
                practicalWorkRecords = practicalWorkById.Values.OrderBy(data => data.recordId, StringComparer.Ordinal).Select(data => data.Clone()).ToList(),
                supervisedWorkRecords = supervisedWorkById.Values.OrderBy(data => data.recordId, StringComparer.Ordinal).Select(data => data.Clone()).ToList()
            };
        }

        public TrainingOperationResult RestoreFromSaveData(TrainingRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, InformationTransferRuntime transferRuntime, IEnumerable<string> persons, bool restoring = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, professionRuntime, persons, out string failure))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.RestoreFailed, failure, before);
            }

            Configure(definitionRegistry, professionRuntime, transferRuntime, persons);
            RestoreInternal(saveData);
            dirty = !restoring;
            return TrainingOperationResult.Success("Training runtime restored without replaying teaching or completion effects.", before, revision);
        }

        public static bool ValidateSaveData(TrainingRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Training save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > TrainingRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported training schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> knownPersons = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            Dictionary<string, TrainingEnrollmentData> enrollments = new Dictionary<string, TrainingEnrollmentData>(StringComparer.Ordinal);
            foreach (TrainingEnrollmentData enrollment in saveData.enrollments ?? new List<TrainingEnrollmentData>())
            {
                if (enrollment == null || string.IsNullOrWhiteSpace(enrollment.enrollmentId) || enrollments.ContainsKey(enrollment.enrollmentId))
                {
                    failure = $"Training save has duplicate or blank enrollment ID '{enrollment?.enrollmentId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(enrollment.personId) || (knownPersons.Count > 0 && !knownPersons.Contains(enrollment.personId)))
                {
                    failure = $"Training enrollment '{enrollment.enrollmentId}' references unknown person '{enrollment.personId}'.";
                    return false;
                }

                if (definitionRegistry == null || !definitionRegistry.TryGet(enrollment.programId, out TrainingProgramDefinition _))
                {
                    failure = $"Training enrollment '{enrollment.enrollmentId}' references missing program '{enrollment.programId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(TrainingEnrollmentState), enrollment.state))
                {
                    failure = $"Training enrollment '{enrollment.enrollmentId}' has invalid state '{enrollment.state}'.";
                    return false;
                }

                if (ParseDouble(enrollment.completionWorldTime) > 0d && ParseDouble(enrollment.completionWorldTime) < ParseDouble(enrollment.startWorldTime))
                {
                    failure = $"Training enrollment '{enrollment.enrollmentId}' completion time predates start time.";
                    return false;
                }

                enrollments.Add(enrollment.enrollmentId, enrollment);
            }

            if (!ValidateLinkedRecords(saveData, enrollments, knownPersons, out failure))
            {
                return false;
            }

            return true;
        }

        private TrainingOperationResult Transition(string enrollmentId, TrainingEnrollmentState next, string transactionId, bool preview, TrainingHistoryHookKind hook, string message)
        {
            long before = revision;
            if (!enrollmentsById.TryGetValue(enrollmentId ?? string.Empty, out TrainingEnrollmentData enrollment))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.MissingEnrollment, $"Training enrollment '{enrollmentId}' is missing.", before);
            }

            if (!IsValidTransition(enrollment.state, next))
            {
                return TrainingOperationResult.Failure(TrainingOperationStatus.InvalidTransition, $"Cannot transition training enrollment from {enrollment.state} to {next}.", before);
            }

            TrainingEnrollmentData working = enrollment.Clone();
            working.state = next;
            working.revision++;
            if (preview)
            {
                return TrainingOperationResult.Success($"{message} Preview only.", before, before, new TrainingEnrollmentSnapshot(working), preview: true);
            }

            enrollmentsById[enrollmentId] = working;
            revision++;
            dirty = true;
            AddHook(hook, working, transactionId, ParseDouble(working.startWorldTime));
            return TrainingOperationResult.Success(message, before, revision, new TrainingEnrollmentSnapshot(working));
        }

        private bool TryResolveEnrollment(string enrollmentId, out TrainingEnrollmentData enrollment, out TrainingProgramDefinition program, out TrainingCurriculumDefinition curriculum)
        {
            enrollment = null;
            program = null;
            curriculum = null;
            if (!ValidateConfigured(out _) || !enrollmentsById.TryGetValue(enrollmentId ?? string.Empty, out TrainingEnrollmentData found))
            {
                return false;
            }

            if (!ResolveProgram(found.programId, out program, out curriculum))
            {
                return false;
            }

            enrollment = found;
            return true;
        }

        private bool ResolveProgram(string programId, out TrainingProgramDefinition program, out TrainingCurriculumDefinition curriculum)
        {
            program = null;
            curriculum = null;
            if (registry == null || !registry.TryGet(programId ?? string.Empty, out program))
            {
                return false;
            }

            return registry.TryGet(program.CurriculumId, out curriculum);
        }

        private bool ValidateConfigured(out string failure)
        {
            failure = string.Empty;
            if (registry == null)
            {
                failure = "Training runtime has no definition registry.";
                return false;
            }

            return true;
        }

        private bool KnownPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        }

        private bool InstructorMeetsRequirement(string instructorPersonId, TrainingInstructorRequirementData requirement, string professionId, string specializationId, string authorityId, out string failure)
        {
            failure = string.Empty;
            if (requirement == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(requirement.requiredProfessionId))
            {
                bool explicitMatch = string.Equals(professionId ?? string.Empty, requirement.requiredProfessionId, StringComparison.Ordinal);
                bool runtimeMatch = professions != null && professions.QueryByPerson(instructorPersonId).Any(snapshot => snapshot.Active && string.Equals(snapshot.ProfessionId, requirement.requiredProfessionId, StringComparison.Ordinal));
                if (!explicitMatch && !runtimeMatch)
                {
                    failure = $"Instructor '{instructorPersonId}' lacks required profession '{requirement.requiredProfessionId}'.";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(requirement.requiredSpecializationId) && !string.Equals(specializationId ?? string.Empty, requirement.requiredSpecializationId, StringComparison.Ordinal))
            {
                failure = $"Instructor '{instructorPersonId}' lacks required specialization '{requirement.requiredSpecializationId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requirement.requiredAuthorityId) && !string.Equals(authorityId ?? string.Empty, requirement.requiredAuthorityId, StringComparison.Ordinal))
            {
                failure = $"Instructor '{instructorPersonId}' lacks required authority '{requirement.requiredAuthorityId}'.";
                return false;
            }

            return true;
        }

        private IEnumerable<string> InstructorPersons(TrainingEnrollmentData enrollment)
        {
            return (enrollment?.instructorAssignmentIds ?? Array.Empty<string>())
                .Where(id => instructorAssignmentsById.ContainsKey(id))
                .Select(id => instructorAssignmentsById[id].personId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal);
        }

        private bool ModuleDependenciesCompleted(TrainingEnrollmentData enrollment, TrainingModuleDefinitionData module, out string failure)
        {
            failure = string.Empty;
            foreach (string dependency in module.dependencyModuleIds ?? Array.Empty<string>())
            {
                if (!enrollment.completedModuleIds.Contains(dependency, StringComparer.Ordinal))
                {
                    failure = $"Dependency module '{dependency}' is incomplete.";
                    return false;
                }
            }

            return true;
        }

        private bool ModuleLessonsCompleted(TrainingEnrollmentData enrollment, TrainingModuleDefinitionData module, out string failure)
        {
            failure = string.Empty;
            foreach (string lessonId in module.lessonIds ?? Array.Empty<string>())
            {
                bool completed = (enrollment.lessonSessionIds ?? Array.Empty<string>())
                    .Where(id => sessionsById.ContainsKey(id))
                    .Select(id => sessionsById[id])
                    .Any(session => string.Equals(session.lessonId, lessonId, StringComparison.Ordinal) && session.state == TrainingSessionCompletionState.Completed);
                if (!completed)
                {
                    failure = $"Lesson '{lessonId}' is incomplete.";
                    return false;
                }
            }

            return true;
        }

        private bool ModuleAssignmentsCompleted(TrainingEnrollmentData enrollment, TrainingModuleDefinitionData module, out string failure)
        {
            failure = string.Empty;
            foreach (string assignmentId in module.assignmentIds ?? Array.Empty<string>())
            {
                bool completed = (enrollment.practicalWorkRecordIds ?? Array.Empty<string>())
                    .Where(id => practicalWorkById.ContainsKey(id))
                    .Select(id => practicalWorkById[id])
                    .Any(record => string.Equals(record.assignmentId, assignmentId, StringComparison.Ordinal) && record.accepted);
                if (!completed)
                {
                    failure = $"Practical assignment '{assignmentId}' is incomplete.";
                    return false;
                }
            }

            return true;
        }

        private TrainingProgressTokenData CreateProgressToken(string enrollmentId, bool perceived)
        {
            string hash = string.Join("|", enrollmentId ?? string.Empty, perceived ? "perceived" : "authoritative", revision.ToString(), professions?.Revision.ToString() ?? "0", transfers?.TransferRevision.ToString() ?? "0");
            return new TrainingProgressTokenData
            {
                trainingRevision = revision,
                professionRevision = professions?.Revision ?? 0L,
                transferRevision = transfers?.TransferRevision ?? 0L,
                contextHash = hash
            };
        }

        private static InformationTransferMode MapTransferMode(TrainingTeachingMethod method)
        {
            return method switch
            {
                TrainingTeachingMethod.Lecture => InformationTransferMode.Lecture,
                TrainingTeachingMethod.Reading => InformationTransferMode.BookReading,
                TrainingTeachingMethod.Demonstration => InformationTransferMode.Demonstration,
                TrainingTeachingMethod.GuidedPractice => InformationTransferMode.GuidedPractice,
                TrainingTeachingMethod.SupervisedWork => InformationTransferMode.Instruction,
                TrainingTeachingMethod.Discussion => InformationTransferMode.ConversationStatement,
                TrainingTeachingMethod.ExaminationPreparation => InformationTransferMode.FormalLesson,
                _ => InformationTransferMode.InformalTeaching
            };
        }

        private static bool IsActive(TrainingEnrollmentState state)
        {
            return state == TrainingEnrollmentState.Applied
                || state == TrainingEnrollmentState.Accepted
                || state == TrainingEnrollmentState.Enrolled
                || state == TrainingEnrollmentState.Active
                || state == TrainingEnrollmentState.Paused
                || state == TrainingEnrollmentState.Suspended;
        }

        private static bool CanPerformLearning(TrainingEnrollmentState state)
        {
            return state == TrainingEnrollmentState.Accepted || state == TrainingEnrollmentState.Enrolled || state == TrainingEnrollmentState.Active;
        }

        private static bool CanComplete(TrainingEnrollmentState state)
        {
            return state == TrainingEnrollmentState.Accepted || state == TrainingEnrollmentState.Enrolled || state == TrainingEnrollmentState.Active || state == TrainingEnrollmentState.Paused;
        }

        private static bool IsTerminal(TrainingEnrollmentState state)
        {
            return state == TrainingEnrollmentState.Withdrawn
                || state == TrainingEnrollmentState.Dismissed
                || state == TrainingEnrollmentState.Failed
                || state == TrainingEnrollmentState.Completed
                || state == TrainingEnrollmentState.Cancelled
                || state == TrainingEnrollmentState.Expired;
        }

        private static bool IsValidTransition(TrainingEnrollmentState current, TrainingEnrollmentState next)
        {
            if (current == next)
            {
                return true;
            }

            if (IsTerminal(current))
            {
                return false;
            }

            return current switch
            {
                TrainingEnrollmentState.Applied => next == TrainingEnrollmentState.Accepted || next == TrainingEnrollmentState.Withdrawn || next == TrainingEnrollmentState.Dismissed || next == TrainingEnrollmentState.Cancelled || next == TrainingEnrollmentState.Expired,
                TrainingEnrollmentState.Accepted => next == TrainingEnrollmentState.Enrolled || next == TrainingEnrollmentState.Active || next == TrainingEnrollmentState.Withdrawn || next == TrainingEnrollmentState.Dismissed || next == TrainingEnrollmentState.Cancelled,
                TrainingEnrollmentState.Enrolled => next == TrainingEnrollmentState.Active || next == TrainingEnrollmentState.Paused || next == TrainingEnrollmentState.Withdrawn || next == TrainingEnrollmentState.Dismissed || next == TrainingEnrollmentState.Failed || next == TrainingEnrollmentState.Completed,
                TrainingEnrollmentState.Active => next == TrainingEnrollmentState.Paused || next == TrainingEnrollmentState.Suspended || next == TrainingEnrollmentState.Withdrawn || next == TrainingEnrollmentState.Dismissed || next == TrainingEnrollmentState.Failed || next == TrainingEnrollmentState.Completed,
                TrainingEnrollmentState.Paused => next == TrainingEnrollmentState.Active || next == TrainingEnrollmentState.Withdrawn || next == TrainingEnrollmentState.Dismissed || next == TrainingEnrollmentState.Failed,
                TrainingEnrollmentState.Suspended => next == TrainingEnrollmentState.Active || next == TrainingEnrollmentState.Dismissed || next == TrainingEnrollmentState.Failed,
                _ => false
            };
        }

        private static string[] AddSorted(IEnumerable<string> source, string value)
        {
            return (source ?? Array.Empty<string>()).Concat(new[] { value }).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        }

        private static string[] RemoveSorted(IEnumerable<string> source, string value)
        {
            return (source ?? Array.Empty<string>()).Where(item => !string.Equals(item, value ?? string.Empty, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, out double parsed) ? parsed : 0d;
        }

        private static TrainingEnrollmentData Redacted(TrainingEnrollmentData source)
        {
            TrainingEnrollmentData data = source.Clone();
            data.personId = string.Empty;
            data.instructorAssignmentIds = Array.Empty<string>();
            data.masterPersonId = string.Empty;
            data.progressSummary = string.Empty;
            data.provenanceId = string.Empty;
            return data;
        }

        private void AddHook(TrainingHistoryHookKind kind, TrainingEnrollmentData enrollment, string transactionId, double worldTime, string relatedId = "")
        {
            historyHooks.Add(new TrainingHistoryHookData
            {
                kind = kind,
                enrollmentId = enrollment?.enrollmentId ?? string.Empty,
                personId = enrollment?.personId ?? string.Empty,
                programId = enrollment?.programId ?? string.Empty,
                relatedId = relatedId ?? string.Empty,
                worldTime = worldTime.ToString("R"),
                transactionId = transactionId ?? string.Empty
            });
        }

        private void RestoreInternal(TrainingRuntimeSaveData saveData)
        {
            enrollmentsById.Clear();
            instructorAssignmentsById.Clear();
            sessionsById.Clear();
            practicalWorkById.Clear();
            supervisedWorkById.Clear();
            exclusiveActivityReferences.Clear();
            foreach (TrainingEnrollmentData data in saveData.enrollments ?? new List<TrainingEnrollmentData>())
            {
                enrollmentsById[data.enrollmentId] = data.Clone();
            }

            foreach (TrainingInstructorAssignmentData data in saveData.instructorAssignments ?? new List<TrainingInstructorAssignmentData>())
            {
                instructorAssignmentsById[data.assignmentId] = data.Clone();
            }

            foreach (TrainingLearningSessionData data in saveData.learningSessions ?? new List<TrainingLearningSessionData>())
            {
                sessionsById[data.sessionId] = data.Clone();
            }

            foreach (TrainingPracticalWorkRecordData data in saveData.practicalWorkRecords ?? new List<TrainingPracticalWorkRecordData>())
            {
                practicalWorkById[data.recordId] = data.Clone();
                if (!string.IsNullOrWhiteSpace(data.activityReferenceId))
                {
                    exclusiveActivityReferences.Add(data.activityReferenceId);
                }
            }

            foreach (TrainingSupervisedWorkRecordData data in saveData.supervisedWorkRecords ?? new List<TrainingSupervisedWorkRecordData>())
            {
                supervisedWorkById[data.recordId] = data.Clone();
            }

            revision = Math.Max(0L, saveData.revision);
            historyHooks.Clear();
        }

        private static bool ValidateLinkedRecords(TrainingRuntimeSaveData saveData, Dictionary<string, TrainingEnrollmentData> enrollments, HashSet<string> knownPersons, out string failure)
        {
            failure = string.Empty;
            if (!Unique(saveData.instructorAssignments, item => item?.assignmentId, out failure, "instructor assignment")) return false;
            if (!Unique(saveData.learningSessions, item => item?.sessionId, out failure, "learning session")) return false;
            if (!Unique(saveData.practicalWorkRecords, item => item?.recordId, out failure, "practical work record")) return false;
            if (!Unique(saveData.supervisedWorkRecords, item => item?.recordId, out failure, "supervised work record")) return false;

            foreach (TrainingInstructorAssignmentData assignment in saveData.instructorAssignments ?? new List<TrainingInstructorAssignmentData>())
            {
                if (!enrollments.ContainsKey(assignment.enrollmentId ?? string.Empty))
                {
                    failure = $"Instructor assignment '{assignment.assignmentId}' references missing enrollment '{assignment.enrollmentId}'.";
                    return false;
                }

                if (knownPersons.Count > 0 && !knownPersons.Contains(assignment.personId ?? string.Empty))
                {
                    failure = $"Instructor assignment '{assignment.assignmentId}' references unknown Person '{assignment.personId}'.";
                    return false;
                }
            }

            HashSet<string> activityReferences = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrainingPracticalWorkRecordData record in saveData.practicalWorkRecords ?? new List<TrainingPracticalWorkRecordData>())
            {
                if (!enrollments.ContainsKey(record.enrollmentId ?? string.Empty))
                {
                    failure = $"Practical work record '{record.recordId}' references missing enrollment '{record.enrollmentId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(record.activityReferenceId) && !activityReferences.Add(record.activityReferenceId))
                {
                    failure = $"Activity reference '{record.activityReferenceId}' is counted by multiple practical work records.";
                    return false;
                }
            }

            foreach (TrainingLearningSessionData session in saveData.learningSessions ?? new List<TrainingLearningSessionData>())
            {
                if (!enrollments.ContainsKey(session.enrollmentId ?? string.Empty))
                {
                    failure = $"Learning session '{session.sessionId}' references missing enrollment '{session.enrollmentId}'.";
                    return false;
                }
            }

            foreach (TrainingSupervisedWorkRecordData record in saveData.supervisedWorkRecords ?? new List<TrainingSupervisedWorkRecordData>())
            {
                if (!enrollments.ContainsKey(record.enrollmentId ?? string.Empty))
                {
                    failure = $"Supervised work record '{record.recordId}' references missing enrollment '{record.enrollmentId}'.";
                    return false;
                }

                if (knownPersons.Count > 0 && (!knownPersons.Contains(record.learnerPersonId ?? string.Empty) || !knownPersons.Contains(record.supervisorPersonId ?? string.Empty)))
                {
                    failure = $"Supervised work record '{record.recordId}' references an unknown learner or supervisor.";
                    return false;
                }

                if (ParseDouble(record.completionWorldTime) > 0d && ParseDouble(record.completionWorldTime) < ParseDouble(record.startWorldTime))
                {
                    failure = $"Supervised work record '{record.recordId}' completion time predates start time.";
                    return false;
                }
            }

            return true;
        }

        private static bool Unique<T>(IEnumerable<T> values, Func<T, string> id, out string failure, string label)
        {
            failure = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                string key = id(value) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || !ids.Add(key))
                {
                    failure = $"Training save has duplicate or blank {label} ID '{key}'.";
                    return false;
                }
            }

            return true;
        }
    }
}
