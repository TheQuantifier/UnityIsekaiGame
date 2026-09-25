using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class EducationTrainingApprenticeshipTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string LearnerId = "person.training.learner";
        private const string MasterId = "person.training.master";
        private const string EnrollmentId = "training-enrollment.test.blacksmith";

        [Test]
        public void PrototypeTrainingDefinitionsValidateAndCurriculumCyclesAreRejected()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(registry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            TrainingCurriculumDefinition cyclic = ScriptableObject.CreateInstance<TrainingCurriculumDefinition>();
            cyclic.DevelopmentConfigure(
                "training-curriculum.test.cycle",
                ProfessionContentIds.BlacksmithApprenticeshipProgramId,
                "Cycle",
                new[]
                {
                    Module("training-module.test.a", true, false, dependencies: new[] { "training-module.test.b" }),
                    Module("training-module.test.b", true, false, dependencies: new[] { "training-module.test.a" })
                },
                Array.Empty<TrainingLessonDefinitionData>());
            DefinitionValidationReport cycleReport = new DefinitionValidationReport();
            cyclic.ValidateCatalogDefinition(registry.DefinitionsById, cycleReport);

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(cycleReport.ErrorCount, Is.GreaterThan(0), cycleReport.GetSummary());
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithApprenticeshipProgramId, out TrainingProgramDefinition program), Is.True);
            Assert.That(program.Category, Is.EqualTo(TrainingProgramCategory.Apprenticeship));
        }

        [Test]
        public void EnrollmentAndApprenticeshipTransitionsDoNotGrantCompetencyOrRecognition()
        {
            using Fixture fixture = new Fixture();
            long professionRevision = fixture.Professions.Revision;
            long knowledgeRevision = fixture.LearnerKnowledge.KnowledgeRevision;

            TrainingOperationResult apply = fixture.Apply();
            TrainingOperationResult duplicate = fixture.Training.ApplyToProgram("training-enrollment.test.blacksmith.second", LearnerId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, "tx.training.duplicate");
            TrainingOperationResult accept = fixture.Training.AcceptEnrollment(EnrollmentId, "tx.training.accept");
            TrainingOperationResult master = fixture.Training.AssignInstructor(EnrollmentId, "training-instructor.test.master", TrainingInstructorRoleKind.Master, MasterId, "tx.training.master", professionId: ProfessionContentIds.BlacksmithProfessionId, authorityId: "authority.guild.prototype");
            TrainingOperationResult begin = fixture.Training.BeginProgram(EnrollmentId, "tx.training.begin");
            TrainingOperationResult invalidAfterTerminal = fixture.Training.Withdraw(EnrollmentId, "tx.training.withdraw");
            TrainingOperationResult terminalBegin = fixture.Training.BeginProgram(EnrollmentId, "tx.training.begin.after-terminal");

            Assert.That(apply.Succeeded, Is.True, apply.Message);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(TrainingOperationStatus.Duplicate));
            Assert.That(accept.Succeeded, Is.True, accept.Message);
            Assert.That(master.Succeeded, Is.True, master.Message);
            Assert.That(master.Enrollment.Data.masterPersonId, Is.EqualTo(MasterId));
            Assert.That(begin.Succeeded, Is.True, begin.Message);
            Assert.That(invalidAfterTerminal.Succeeded, Is.True, invalidAfterTerminal.Message);
            Assert.That(terminalBegin.Succeeded, Is.False);
            Assert.That(terminalBegin.Status, Is.EqualTo(TrainingOperationStatus.InvalidTransition));
            Assert.That(fixture.Professions.Revision, Is.EqualTo(professionRevision));
            Assert.That(fixture.LearnerKnowledge.KnowledgeRevision, Is.EqualTo(knowledgeRevision));
        }

        [Test]
        public void LearningSessionUsesStep8TeachingButAttendanceAloneDoesNotGuaranteeKnowledge()
        {
            using Fixture fixture = new Fixture();
            fixture.BeginApprenticeship();
            TrainingOperationResult attended = fixture.Training.RunLearningSession("training-session.test.attendance", EnrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, "tx.training.attendance", startWorldTime: 10d, completionWorldTime: 11d);
            long knowledgeBeforeTeaching = fixture.LearnerKnowledge.KnowledgeRevision;
            Assert.That(attended.Succeeded, Is.True, attended.Message);
            Assert.That(fixture.LearnerKnowledge.KnowledgeRevision, Is.EqualTo(knowledgeBeforeTeaching));

            TrainingOperationResult taught = fixture.Training.RunLearningSession(
                "training-session.test.teaching",
                EnrollmentId,
                ProfessionContentIds.BlacksmithPracticeModuleId,
                ProfessionContentIds.BlacksmithDemonstrationLessonId,
                "tx.training.teaching",
                fixture.TeachingRequest("tx.training.teaching.transfer"),
                startWorldTime: 12d,
                completionWorldTime: 13d);

            Assert.That(taught.Succeeded, Is.True, taught.Message);
            Assert.That(taught.Transfer, Is.Not.Null);
            Assert.That(taught.Transfer.Succeeded, Is.True, taught.Transfer.Message);
            Assert.That(taught.Transfer.Record.Data.teachingRequested, Is.True);
            Assert.That(fixture.LearnerKnowledge.KnowledgeRevision, Is.GreaterThan(knowledgeBeforeTeaching));
        }

        [Test]
        public void PracticalAssignmentsAndSupervisedWorkReferenceRealActivityRecords()
        {
            using Fixture fixture = new Fixture();
            fixture.BeginApprenticeship();
            fixture.CompleteBasics();
            fixture.RunPracticeLesson();

            TrainingOperationResult missingSupervisor = fixture.Training.RecordPracticalAssignment("training-practical.test.missing-supervisor", EnrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, "crafting-operation.prototype.blacksmith.001", TrainingAssignmentActivityCategory.Crafting, "tx.training.practice.missing", supervisorPersonId: "");
            TrainingOperationResult accepted = fixture.Training.RecordPracticalAssignment("training-practical.test.accepted", EnrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, "crafting-operation.prototype.blacksmith.001", TrainingAssignmentActivityCategory.Crafting, "tx.training.practice.accepted", quality: 700, supervisorPersonId: MasterId);
            TrainingOperationResult duplicateActivity = fixture.Training.RecordPracticalAssignment("training-practical.test.duplicate", EnrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, "crafting-operation.prototype.blacksmith.001", TrainingAssignmentActivityCategory.Crafting, "tx.training.practice.duplicate", quality: 700, supervisorPersonId: MasterId);
            TrainingOperationResult supervised = fixture.Training.RecordSupervisedWork("training-supervised.test.forge", EnrollmentId, MasterId, ProfessionContentIds.BlacksmithProfessionId, "crafting-operation.prototype.blacksmith.001", TrainingSupervisionLevel.CloselySupervised, TrainingWorkOutcome.Succeeded, "tx.training.supervised", quality: 725, startWorldTime: 20d, completionWorldTime: 21d);

            Assert.That(missingSupervisor.Succeeded, Is.False);
            Assert.That(missingSupervisor.Status, Is.EqualTo(TrainingOperationStatus.RequirementBlocked));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(duplicateActivity.Succeeded, Is.False);
            Assert.That(duplicateActivity.Status, Is.EqualTo(TrainingOperationStatus.DuplicateActivity));
            Assert.That(supervised.Succeeded, Is.True, supervised.Message);
            Assert.That(fixture.Training.CreateSaveData().practicalWorkRecords.Single().activityReferenceId, Is.EqualTo("crafting-operation.prototype.blacksmith.001"));
            Assert.That(fixture.Training.CreateSaveData().supervisedWorkRecords.Single().supervisionLevel, Is.EqualTo(TrainingSupervisionLevel.CloselySupervised));
        }

        [Test]
        public void AuthoritativeAndPerceivedProgressDifferAndCompletionRejectsStaleTokens()
        {
            using Fixture fixture = new Fixture();
            fixture.BeginApprenticeship();
            fixture.CompleteVisibleRequirements();
            TrainingProgressResult perceived = fixture.Training.EvaluateProgress(EnrollmentId, perceived: true);
            TrainingProgressResult authoritative = fixture.Training.EvaluateProgress(EnrollmentId, perceived: false);
            TrainingProgressTokenData staleToken = authoritative.RuntimeToken;
            TrainingOperationResult blocked = fixture.Training.CompleteProgram(EnrollmentId, "tx.training.complete.blocked", authoritative.RuntimeToken);
            TrainingOperationResult hidden = fixture.Training.CompleteModule(EnrollmentId, ProfessionContentIds.BlacksmithHiddenAssessmentModuleId, "tx.training.hidden");
            TrainingOperationResult stale = fixture.Training.CompleteProgram(EnrollmentId, "tx.training.complete.stale", staleToken);
            TrainingProgressResult current = fixture.Training.EvaluateProgress(EnrollmentId, perceived: false);
            TrainingOperationResult complete = fixture.Training.CompleteProgram(EnrollmentId, "tx.training.complete", current.RuntimeToken, worldTime: 100d);

            Assert.That(perceived.EligibleForCompletion, Is.True);
            Assert.That(authoritative.EligibleForCompletion, Is.False);
            Assert.That(authoritative.RemainingRequirements, Does.Contain(ProfessionContentIds.BlacksmithHiddenAssessmentModuleId));
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Status, Is.EqualTo(TrainingOperationStatus.RequirementBlocked));
            Assert.That(hidden.Succeeded, Is.True, hidden.Message);
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Status, Is.EqualTo(TrainingOperationStatus.StaleProgress));
            Assert.That(complete.Succeeded, Is.True, complete.Message);
            Assert.That(complete.Enrollment.State, Is.EqualTo(TrainingEnrollmentState.Completed));
            Assert.That(fixture.Professions.Count, Is.Zero);
        }

        [Test]
        public void PersistenceRoundTripAndCorruptRestoreAreAtomicAndSilent()
        {
            using Fixture fixture = new Fixture();
            fixture.BeginApprenticeship();
            fixture.CompleteVisibleRequirements();
            TrainingRuntimeSaveData save = fixture.Training.CreateSaveData();
            TrainingRuntime restored = fixture.NewTrainingRuntime();

            TrainingOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Professions, fixture.Transfers, fixture.Persons, restoring: true);
            TrainingRuntimeSaveData corrupt = save.Clone();
            corrupt.enrollments[0].programId = "training.program.missing";
            int beforeCount = restored.EnrollmentCount;
            long beforeRevision = restored.Revision;
            TrainingOperationResult rejected = restored.RestoreFromSaveData(corrupt, fixture.Registry, fixture.Professions, fixture.Transfers, fixture.Persons, restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.EnrollmentCount, Is.EqualTo(fixture.Training.EnrollmentCount));
            Assert.That(restored.Revision, Is.EqualTo(fixture.Training.Revision));
            Assert.That(restored.HistoryHooks, Is.Empty);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.EnrollmentCount, Is.EqualTo(beforeCount));
            Assert.That(restored.Revision, Is.EqualTo(beforeRevision));
        }

        [Test]
        public void PersistenceParticipantCapturesAndCommitsTrainingState()
        {
            using Fixture fixture = new Fixture();
            fixture.BeginApprenticeship();
            TrainingPersistenceParticipant participant = new TrainingPersistenceParticipant(fixture.Training, () => fixture.Registry, () => fixture.Professions, () => fixture.Transfers, () => fixture.Persons);
            PersistenceParticipantSaveResult capture = participant.CapturePayload();

            TrainingRuntime restoredRuntime = fixture.NewTrainingRuntime();
            TrainingPersistenceParticipant restoredParticipant = new TrainingPersistenceParticipant(restoredRuntime, () => fixture.Registry, () => fixture.Professions, () => fixture.Transfers, () => fixture.Persons);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(capture.PayloadJson, TrainingPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);

            Assert.That(capture.Succeeded, Is.True, capture.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredRuntime.TryGetEnrollment(EnrollmentId, out TrainingEnrollmentSnapshot enrollment), Is.True);
            Assert.That(enrollment.State, Is.EqualTo(TrainingEnrollmentState.Active));
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }

        private static KnowledgePropositionData Proposition()
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                subjectType = KnowledgeSubjectType.Species,
                subjectId = "species.human",
                valueType = KnowledgeValueType.StableId,
                stableValueId = "capability.profession.blacksmith-safety"
            };
        }

        private static TrainingModuleDefinitionData Module(string id, bool required, bool hidden, string[] dependencies = null)
        {
            return new TrainingModuleDefinitionData
            {
                moduleId = id,
                displayName = id,
                required = required,
                hiddenFromLearner = hidden,
                dependencyModuleIds = dependencies ?? Array.Empty<string>()
            };
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject learnerObject = new GameObject("Training Learner");
            private readonly GameObject masterObject = new GameObject("Training Master");
            private readonly AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();

            public Fixture()
            {
                Registry = CreateRegistry();
                Persons = new[] { LearnerId, MasterId };
                history.Configure(Registry, "world.training.tests", Persons);
                LearnerKnowledge = learnerObject.AddComponent<PersonKnowledgeRuntime>();
                MasterKnowledge = masterObject.AddComponent<PersonKnowledgeRuntime>();
                LearnerKnowledge.Configure(Registry, LearnerId);
                MasterKnowledge.Configure(Registry, MasterId);
                LearnerMemory = new PersonMemoryRuntime();
                MasterMemory = new PersonMemoryRuntime();
                LearnerMemory.Configure(LearnerId, Registry, history, Persons);
                MasterMemory.Configure(MasterId, Registry, history, Persons);
                Transfers = new InformationTransferRuntime();
                Transfers.Configure(Registry, MasterId);
                Professions = new PersonProfessionRuntime();
                Professions.Configure(Registry, Persons);
                Training = NewTrainingRuntime();
                RecordMasterKnowledge();
            }

            public DefinitionRegistry Registry { get; }
            public string[] Persons { get; }
            public PersonKnowledgeRuntime LearnerKnowledge { get; }
            public PersonKnowledgeRuntime MasterKnowledge { get; }
            public PersonMemoryRuntime LearnerMemory { get; }
            public PersonMemoryRuntime MasterMemory { get; }
            public InformationTransferRuntime Transfers { get; }
            public PersonProfessionRuntime Professions { get; }
            public TrainingRuntime Training { get; }

            public TrainingRuntime NewTrainingRuntime()
            {
                TrainingRuntime runtime = new TrainingRuntime();
                runtime.Configure(Registry, Professions, Transfers, Persons);
                return runtime;
            }

            public TrainingOperationResult Apply()
            {
                return Training.ApplyToProgram(EnrollmentId, LearnerId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, "tx.training.apply", worldTime: 1d);
            }

            public void BeginApprenticeship()
            {
                Assert.That(Apply().Succeeded, Is.True);
                Assert.That(Training.AcceptEnrollment(EnrollmentId, "tx.training.accept").Succeeded, Is.True);
                Assert.That(Training.AssignInstructor(EnrollmentId, "training-instructor.test.master", TrainingInstructorRoleKind.Master, MasterId, "tx.training.master", professionId: ProfessionContentIds.BlacksmithProfessionId, authorityId: "authority.guild.prototype").Succeeded, Is.True);
                Assert.That(Training.BeginProgram(EnrollmentId, "tx.training.begin").Succeeded, Is.True);
            }

            public void CompleteBasics()
            {
                TrainingOperationResult attendance = Training.RunLearningSession("training-session.test.safety", EnrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, "tx.training.safety");
                Assert.That(attendance.Succeeded, Is.True, attendance.Message);
                TrainingOperationResult basics = Training.CompleteModule(EnrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, "tx.training.module.basics");
                Assert.That(basics.Succeeded, Is.True, basics.Message);
            }

            public void RunPracticeLesson()
            {
                TrainingOperationResult session = Training.RunLearningSession("training-session.test.practice", EnrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, ProfessionContentIds.BlacksmithDemonstrationLessonId, "tx.training.practice.lesson");
                Assert.That(session.Succeeded, Is.True, session.Message);
            }

            public void CompleteVisibleRequirements()
            {
                CompleteBasics();
                RunPracticeLesson();
                TrainingOperationResult practical = Training.RecordPracticalAssignment("training-practical.test.complete", EnrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, "crafting-operation.prototype.blacksmith.complete", TrainingAssignmentActivityCategory.Crafting, "tx.training.practice.complete", quality: 700, supervisorPersonId: MasterId);
                Assert.That(practical.Succeeded, Is.True, practical.Message);
                TrainingOperationResult practice = Training.CompleteModule(EnrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, "tx.training.module.practice");
                Assert.That(practice.Succeeded, Is.True, practice.Message);
            }

            public InformationTransferRequest TeachingRequest(string transactionId)
            {
                return new InformationTransferRequest
                {
                    TransactionId = transactionId,
                    TransferId = $"transfer.{transactionId}",
                    SenderPersonId = MasterId,
                    RecipientPersonIds = new[] { LearnerId },
                    ContentItems = new[]
                    {
                        new TransferContentItemData
                        {
                            contentItemId = "content.training.blacksmith",
                            contentType = InformationTransferContentType.InstructionalConcept,
                            domain = KnowledgeDomain.Professional,
                            proposition = Proposition(),
                            senderConfidence = 900,
                            senderBeliefState = KnowledgeBeliefState.Known,
                            privacyClassification = KnowledgeVisibility.Public,
                            assertionType = InformationTransferAssertionType.Instruction,
                            typedPayloadId = "procedure.blacksmith.forge-safety",
                            rawEvidenceStrength = 850
                        }
                    },
                    WorldTimeSeconds = 12d,
                    PrivacyScope = TransferPrivacyScope.RecipientOnly,
                    SenderKnowledge = MasterKnowledge,
                    SenderMemory = MasterMemory,
                    SourceRuntime = null,
                    RecipientKnowledgeRuntimes = new Dictionary<string, PersonKnowledgeRuntime> { [LearnerId] = LearnerKnowledge },
                    RecipientMemoryRuntimes = new Dictionary<string, PersonMemoryRuntime> { [LearnerId] = LearnerMemory },
                    PrivilegedAccess = true
                };
            }

            private void RecordMasterKnowledge()
            {
                KnowledgeOperationResult result = MasterKnowledge.RecordObservation(new KnowledgeObservationRequest
                {
                    PersonId = MasterId,
                    TransactionId = "tx.training.master.knowledge",
                    Proposition = Proposition(),
                    AcquisitionSource = KnowledgeAcquisitionSource.DirectObservation,
                    Provenance = KnowledgeProvenance.DirectObservation,
                    Direction = KnowledgeEvidenceDirection.Supports,
                    Strength = 950,
                    Credibility = 950,
                    SourceId = "training.fixture",
                    Visibility = KnowledgeVisibility.Public,
                    PrivateAccessAuthorized = true
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(learnerObject);
                UnityEngine.Object.DestroyImmediate(masterObject);
            }
        }
    }
}
