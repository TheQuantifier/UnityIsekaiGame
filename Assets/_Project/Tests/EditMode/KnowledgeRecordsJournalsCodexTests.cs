using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Development;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class KnowledgeRecordsJournalsCodexTests
    {
        [Test]
        public void Definitions_ValidateWithCanonicalStableIds()
        {
            KnowledgeRecordDefinition[] definitions =
            {
                Definition("record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, new[] { InformationSubjectType.HistoricalEvent }, new[] { KnowledgeRecordOwnerKind.Person }),
                Definition("record-definition.medical-record", KnowledgeRecordCategory.MedicalRecord, new[] { InformationSubjectType.Diagnosis }, new[] { KnowledgeRecordOwnerKind.Person })
            };
            DefinitionValidationReport report = new DefinitionValidationReport();
            var byId = definitions.Cast<IGameDefinition>().ToDictionary(definition => definition.Id, definition => definition, StringComparer.Ordinal);

            foreach (KnowledgeRecordDefinition definition in definitions)
            {
                definition.ValidateCatalogDefinition(byId, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());
        }

        [Test]
        public void ExplicitRecord_CreatePreviewDuplicateAndSnapshotAreNonMutating()
        {
            KnowledgeRecordRuntime runtime = Runtime(out _);
            KnowledgeRecordCreateRequest request = Request("record.test.journal", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.test.1");
            request.Preview = true;

            KnowledgeRecordOperationResult preview = runtime.CreateRecord(request);
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(runtime.CreateSnapshot().Records.Count, Is.EqualTo(0));

            request.Preview = false;
            KnowledgeRecordOperationResult created = runtime.CreateRecord(request);
            KnowledgeRecordOperationResult duplicate = runtime.CreateRecord(request);
            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(runtime.CreateSnapshot().Records.Count, Is.EqualTo(1));

            KnowledgeRecordSnapshot snapshot = runtime.CreateSnapshot();
            snapshot.Records[0].Data.details[0].value = "mutated through snapshot";
            KnowledgeRecordProjection projected = runtime.ProjectRecord("record.test.journal", PrivilegedContext());
            Assert.That(projected.VisibleDetails[0].value, Is.EqualTo("summary"));
        }

        [Test]
        public void AccessAwareProjection_RedactsAndDoesNotLeakDeniedRecord()
        {
            KnowledgeRecordRuntime runtime = Runtime(out DefinitionRegistry registry);
            InformationAccessRuntime access = new InformationAccessRuntime();
            access.Configure(registry, "person.owner");
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = "information-access.policy.record.secret",
                subject = Subject(InformationSubjectType.KnowledgeRecord, "record.test.secret"),
                classification = InformationVisibilityClassification.Secret,
                disclosurePolicy = InformationDisclosurePolicy.RedactedOnly,
                resharingPolicy = InformationResharingPolicy.NoResharing,
                sourceVisibilityPolicy = InformationSourceVisibilityPolicy.HideOriginal,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                auditPolicy = InformationAuditPolicy.AuditDenied,
                allowedPersonIds = new[] { "person.owner" },
                defaultVisibleDetails = new[] { "detail.summary" },
                defaultRedactedDetails = new[] { "detail.body" },
                provenance = "test"
            }, "tx.policy.secret");

            KnowledgeRecordCreateRequest request = Request("record.test.secret", "record-definition.custom-entry", KnowledgeRecordCategory.Custom, InformationSubjectType.KnowledgeRecord, "record.test.secret");
            request.AccessPolicyId = "information-access.policy.record.secret";
            request.Classification = InformationVisibilityClassification.Secret;
            Assert.That(runtime.CreateRecord(request).Succeeded, Is.True);

            KnowledgeRecordProjection denied = runtime.ProjectRecord("record.test.secret", PublicContext("person.visitor"), access);
            KnowledgeRecordProjection owner = runtime.ProjectRecord("record.test.secret", PublicContext("person.owner"), access);

            Assert.That(denied.Record, Is.Null);
            Assert.That(denied.VisibleRecordId, Is.Empty);
            Assert.That(owner.Succeeded, Is.True);
            Assert.That(owner.Redacted, Is.True);
            Assert.That(owner.VisibleDetails.Any(detail => detail.detailId == "detail.summary"), Is.True);
        }

        [Test]
        public void Corrections_PreserveOriginalAndCollectionsDoNotOwnRecords()
        {
            KnowledgeRecordRuntime runtime = Runtime(out _);
            Assert.That(runtime.CreateRecord(Request("record.test.original", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.Claim, "claim.test")).Succeeded, Is.True);
            KnowledgeRecordCreateRequest correction = Request("record.test.correction", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.Claim, "claim.test");

            KnowledgeRecordOperationResult corrected = runtime.CorrectRecord(correction, "record.test.original");
            KnowledgeRecordOperationResult collection = runtime.CreateCollection("record-collection.test.codex", "Codex", "person.owner", new[] { "record.test.original", "record.test.correction" }, "tx.collection");
            KnowledgeRecordOperationResult removed = runtime.RemoveRecordFromCollection("record-collection.test.codex", "record.test.original", "tx.collection.remove");

            Assert.That(corrected.Succeeded, Is.True, corrected.Message);
            Assert.That(collection.Succeeded, Is.True, collection.Message);
            Assert.That(removed.Succeeded, Is.True, removed.Message);
            KnowledgeRecordSnapshot snapshot = runtime.CreateSnapshot();
            Assert.That(snapshot.Records.Single(record => record.RecordId == "record.test.original").Status, Is.EqualTo(KnowledgeRecordStatus.Corrected));
            Assert.That(snapshot.Records.Any(record => record.RecordId == "record.test.original"), Is.True);
            Assert.That(snapshot.Collections.Single().RecordIds, Is.EquivalentTo(new[] { "record.test.correction" }));
        }

        [Test]
        public void Search_IsDeterministicByTimeAndStableId()
        {
            KnowledgeRecordRuntime runtime = Runtime(out _);
            KnowledgeRecordCreateRequest later = Request("record.test.b", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.b", worldTime: 20d);
            KnowledgeRecordCreateRequest earlierB = Request("record.test.c", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.c", worldTime: 10d);
            KnowledgeRecordCreateRequest earlierA = Request("record.test.a", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.a", worldTime: 10d);
            Assert.That(runtime.CreateRecord(later).Succeeded, Is.True);
            Assert.That(runtime.CreateRecord(earlierB).Succeeded, Is.True);
            Assert.That(runtime.CreateRecord(earlierA).Succeeded, Is.True);

            string[] ids = runtime.Search(new KnowledgeRecordSearchQuery { Category = KnowledgeRecordCategory.PersonalJournal }, PrivilegedContext())
                .Select(projection => projection.VisibleRecordId)
                .ToArray();

            Assert.That(ids, Is.EqualTo(new[] { "record.test.a", "record.test.c", "record.test.b" }));
        }

        [Test]
        public void SaveRestore_PrepareRejectsCorruptPayloadWithoutPartialMutation()
        {
            KnowledgeRecordRuntime runtime = Runtime(out DefinitionRegistry registry);
            Assert.That(runtime.CreateRecord(Request("record.test.persisted", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.persisted")).Succeeded, Is.True);
            KnowledgeRecordSaveData valid = runtime.CreateSaveData();
            KnowledgeRecordRuntime restored = new KnowledgeRecordRuntime();
            restored.Configure(registry, "person.owner");
            Assert.That(restored.RestoreFromSaveData(valid, registry, "person.owner", restoring: true).Succeeded, Is.True);

            KnowledgeRecordSaveData corrupt = restored.CreateSaveData();
            corrupt.records = corrupt.records.Concat(corrupt.records.Take(1).Select(record => record.Clone())).ToArray();
            KnowledgeRecordSnapshot before = restored.CreateSnapshot();
            KnowledgeRecordOperationResult rejected = restored.RestoreFromSaveData(corrupt, registry, "person.owner", restoring: true);
            KnowledgeRecordSnapshot after = restored.CreateSnapshot();

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Records.Select(record => record.RecordId), Is.EqualTo(before.Records.Select(record => record.RecordId)));
        }

        [Test]
        public void PersistenceParticipant_CapturesPrototypeRecordsWithSharedPrototypeDefinitions()
        {
            DefinitionRegistry registry = PrototypeRecordRegistry();
            KnowledgeRecordRuntime runtime = new KnowledgeRecordRuntime();
            runtime.Configure(registry, "person.owner");
            Assert.That(runtime.CreateRecord(Request("record.test.biography", "record-definition.biography-entry", KnowledgeRecordCategory.Biography, InformationSubjectType.PersonIdentity, "person.owner")).Succeeded, Is.True);
            KnowledgeRecordPersistenceParticipant participant = new KnowledgeRecordPersistenceParticipant(runtime, () => registry, "player.local");

            PersistenceParticipantSaveResult capture = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(capture.PayloadJson, KnowledgeRecordPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(capture.Succeeded, Is.True, capture.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            participant.DiscardPreparedPayload(prepare.PreparedPayload);
        }

        [Test]
        public void PersistenceParticipant_StillRejectsPrototypeRecordsWhenDefinitionsAreMissing()
        {
            DefinitionRegistry validRegistry = PrototypeRecordRegistry();
            DefinitionRegistry missingRegistry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            KnowledgeRecordRuntime runtime = new KnowledgeRecordRuntime();
            runtime.Configure(validRegistry, "person.owner");
            Assert.That(runtime.CreateRecord(Request("record.test.missing-definition", "record-definition.biography-entry", KnowledgeRecordCategory.Biography, InformationSubjectType.PersonIdentity, "person.owner")).Succeeded, Is.True);
            KnowledgeRecordPersistenceParticipant participant = new KnowledgeRecordPersistenceParticipant(runtime, () => missingRegistry, "player.local");
            long revision = runtime.RecordRevision;

            PersistenceParticipantSaveResult capture = participant.CapturePayload();

            Assert.That(capture.Succeeded, Is.False);
            Assert.That(capture.Message, Does.Contain("record-definition.biography-entry"));
            Assert.That(runtime.RecordRevision, Is.EqualTo(revision));
        }

        [Test]
        public void ReadAsPerson_CreatesSourceEvidenceAndMemoryWithoutForcingTruth()
        {
            using ReadFixture fixture = ReadFixture.Create();
            Assert.That(fixture.Records.CreateRecord(Request("record.test.read", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.test.read")).Succeeded, Is.True);

            KnowledgeRecordReadResult result = fixture.Records.ReadRecordAsPerson(fixture.ReadRequest("tx.record.read", "record.test.read"), null, fixture.Sources, fixture.Knowledge, fixture.Memory);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.SourceInstanceId, Is.Not.Empty);
            Assert.That(result.EvidenceId, Is.Not.Empty);
            Assert.That(result.MemoryId, Is.Not.Empty);
            Assert.That(fixture.Sources.CreateSnapshot().Sources.Count, Is.EqualTo(1));
            Assert.That(fixture.Knowledge.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
            Assert.That(fixture.Memory.CreateSnapshot().Memories.Count, Is.EqualTo(1));
            Assert.That(result.KnowledgeResult.ResultingBelief.TruthState, Is.EqualTo(KnowledgeTruthState.NotCompared));
            Assert.That(result.KnowledgeResult.ResultingBelief.State, Is.Not.EqualTo(KnowledgeBeliefState.Known));
        }

        [Test]
        public void ProjectionPreviewAndPrivilegedInspectionHaveNoReadSideEffects()
        {
            using ReadFixture fixture = ReadFixture.Create();
            Assert.That(fixture.Records.CreateRecord(Request("record.test.preview-read", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.test.preview-read")).Succeeded, Is.True);
            long sourceRevision = fixture.Sources.SourceRevision;
            long knowledgeRevision = fixture.Knowledge.KnowledgeRevision;
            long memoryRevision = fixture.Memory.MemoryRevision;
            long recordRevision = fixture.Records.RecordRevision;

            KnowledgeRecordProjection projection = fixture.Records.ProjectRecord("record.test.preview-read", PrivilegedContext());
            KnowledgeRecordReadRequest request = fixture.ReadRequest("tx.record.preview-read", "record.test.preview-read");
            request.Preview = true;
            KnowledgeRecordReadResult preview = fixture.Records.ReadRecordAsPerson(request, null, fixture.Sources, fixture.Knowledge, fixture.Memory);
            request = fixture.ReadRequest("tx.record.inspect-read", "record.test.preview-read");
            request.PrivilegedInspection = true;
            KnowledgeRecordReadResult inspection = fixture.Records.ReadRecordAsPerson(request, null, fixture.Sources, fixture.Knowledge, fixture.Memory);

            Assert.That(projection.Succeeded, Is.True);
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(inspection.Succeeded, Is.True, inspection.Message);
            Assert.That(fixture.Sources.SourceRevision, Is.EqualTo(sourceRevision));
            Assert.That(fixture.Knowledge.KnowledgeRevision, Is.EqualTo(knowledgeRevision));
            Assert.That(fixture.Memory.MemoryRevision, Is.EqualTo(memoryRevision));
            Assert.That(fixture.Records.RecordRevision, Is.EqualTo(recordRevision));
        }

        [Test]
        public void ReadAsPerson_RollsBackSourceWhenKnowledgeEvidenceFails()
        {
            using ReadFixture fixture = ReadFixture.Create();
            Assert.That(fixture.Records.CreateRecord(Request("record.test.rollback", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.test.rollback")).Succeeded, Is.True);
            using ReadFixture badKnowledge = ReadFixture.Create(new DefinitionRegistry(new IGameDefinition[]
            {
                Definition("record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, new[] { InformationSubjectType.HistoricalEvent, InformationSubjectType.Claim }, new[] { KnowledgeRecordOwnerKind.Person })
            }));
            long sourceRevision = fixture.Sources.SourceRevision;
            int sourceCount = fixture.Sources.CreateSnapshot().Sources.Count;
            long memoryRevision = fixture.Memory.MemoryRevision;

            KnowledgeRecordReadRequest request = fixture.ReadRequest("tx.record.rollback", "record.test.rollback");
            request.RequireEvidenceProposition = true;
            KnowledgeRecordReadResult result = fixture.Records.ReadRecordAsPerson(request, null, fixture.Sources, badKnowledge.Knowledge, fixture.Memory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(KnowledgeRecordResultCode.PartialMutationRejected));
            Assert.That(fixture.Sources.SourceRevision, Is.EqualTo(sourceRevision));
            Assert.That(fixture.Sources.CreateSnapshot().Sources.Count, Is.EqualTo(sourceCount));
            Assert.That(fixture.Memory.MemoryRevision, Is.EqualTo(memoryRevision));
        }

        [Test]
        public void ReadAsPerson_ReinforcesAndRecoversExistingForgottenReadMemory()
        {
            using ReadFixture fixture = ReadFixture.Create();
            Assert.That(fixture.Records.CreateRecord(Request("record.test.recover", "record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, InformationSubjectType.HistoricalEvent, "event.test.recover")).Succeeded, Is.True);
            KnowledgeRecordReadResult first = fixture.Records.ReadRecordAsPerson(fixture.ReadRequest("tx.record.recover.first", "record.test.recover"), null, fixture.Sources, fixture.Knowledge, fixture.Memory);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(fixture.Memory.ForgetMemory(first.MemoryId, "tx.record.recover.forget").Succeeded, Is.True);
            Assert.That(fixture.Memory.TryGetMemory(first.MemoryId, out HistoryMemoryRecord forgotten), Is.True);
            Assert.That(forgotten.State, Is.EqualTo(MemoryState.Forgotten));

            KnowledgeRecordReadResult second = fixture.Records.ReadRecordAsPerson(fixture.ReadRequest("tx.record.recover.second", "record.test.recover"), null, fixture.Sources, fixture.Knowledge, fixture.Memory);

            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(fixture.Memory.TryGetMemory(first.MemoryId, out HistoryMemoryRecord recovered), Is.True);
            Assert.That(recovered.State, Is.EqualTo(MemoryState.Accessible));
        }

        private static KnowledgeRecordRuntime Runtime(out DefinitionRegistry registry)
        {
            registry = KnowledgeTestDefinitionFactory.AddSourceDefinitions(new IGameDefinition[]
            {
                Definition("record-definition.journal-entry", KnowledgeRecordCategory.PersonalJournal, new[] { InformationSubjectType.HistoricalEvent, InformationSubjectType.Claim }, new[] { KnowledgeRecordOwnerKind.Person }),
                Definition("record-definition.custom-entry", KnowledgeRecordCategory.Custom, new[] { InformationSubjectType.KnowledgeRecord }, new[] { KnowledgeRecordOwnerKind.Person }),
                Definition("record-definition.medical-record", KnowledgeRecordCategory.MedicalRecord, new[] { InformationSubjectType.Diagnosis }, new[] { KnowledgeRecordOwnerKind.Person }),
                Fact(BuiltInKnowledgeFacts.EventOccurred, "Event Occurred", KnowledgeDomain.Historical, KnowledgePropositionType.Event, KnowledgeSubjectType.Event, KnowledgeValueType.Boolean),
                Fact(BuiltInKnowledgeFacts.PersonIdentity, "Person Identity", KnowledgeDomain.Personal, KnowledgePropositionType.Identity, KnowledgeSubjectType.Person, KnowledgeValueType.StableId)
            });
            KnowledgeRecordRuntime runtime = new KnowledgeRecordRuntime();
            runtime.Configure(registry, "person.owner");
            return runtime;
        }

        private static DefinitionRegistry PrototypeRecordRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeTestLabService.PrototypeCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }

        private static KnowledgeRecordDefinition Definition(string id, KnowledgeRecordCategory category, InformationSubjectType[] subjects, KnowledgeRecordOwnerKind[] owners)
        {
            KnowledgeRecordDefinition definition = ScriptableObject.CreateInstance<KnowledgeRecordDefinition>();
            definition.DevelopmentConfigure(id, id, category, subjects, owners);
            return definition;
        }

        private static KnowledgeFactDefinition Fact(string id, string displayName, KnowledgeDomain domain, KnowledgePropositionType propositionType, KnowledgeSubjectType subjectType, KnowledgeValueType valueType)
        {
            KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            definition.name = displayName;
            Set(definition, "factId", id);
            Set(definition, "displayName", displayName);
            Set(definition, "domain", domain);
            Set(definition, "propositionType", propositionType);
            Set(definition, "subjectType", subjectType);
            Set(definition, "valueType", valueType);
            Set(definition, "defaultVisibility", KnowledgeVisibility.Public);
            Set(definition, "stalenessPolicy", KnowledgeStalenessPolicy.HistoricalOnly);
            Set(definition, "certaintyThreshold", 700);
            Set(definition, "requiredEvidenceCount", 1);
            return definition;
        }

        private static void Set<T>(KnowledgeFactDefinition definition, string fieldName, T value)
        {
            FieldInfo field = typeof(KnowledgeFactDefinition).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(definition, value);
        }

        private static KnowledgeRecordCreateRequest Request(string recordId, string definitionId, KnowledgeRecordCategory category, InformationSubjectType subjectType, string subjectId, double worldTime = 10d)
        {
            return new KnowledgeRecordCreateRequest
            {
                TransactionId = "tx." + recordId,
                RecordId = recordId,
                DefinitionId = definitionId,
                Category = category,
                OwnerKind = KnowledgeRecordOwnerKind.Person,
                OwnerId = "person.owner",
                Subject = Subject(subjectType, subjectId),
                AuthorPersonId = "person.owner",
                WorldTimeSeconds = worldTime,
                OccurredWorldTimeSeconds = worldTime,
                Classification = InformationVisibilityClassification.Public,
                Details = new[]
                {
                    new KnowledgeRecordDetailData { detailId = "detail.summary", labelKey = "summary", value = "summary", valueType = KnowledgeValueType.Text },
                    new KnowledgeRecordDetailData { detailId = "detail.body", labelKey = "body", value = "body", valueType = KnowledgeValueType.Text }
                }
            };
        }

        private static InformationSubjectReferenceData Subject(InformationSubjectType type, string id)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = type,
                subjectId = id,
                ownerPersonId = "person.owner"
            };
        }

        private static KnowledgeRecordProjectionContext PrivilegedContext()
        {
            return new KnowledgeRecordProjectionContext
            {
                RequesterPersonId = "person.owner",
                ContextKind = KnowledgeRecordProjectionContextKind.Privileged
            };
        }

        private static KnowledgeRecordProjectionContext PublicContext(string requester)
        {
            return new KnowledgeRecordProjectionContext
            {
                RequesterPersonId = requester,
                ContextKind = KnowledgeRecordProjectionContextKind.Public,
                AccessContext = new InformationAccessContext
                {
                    RequestingPersonId = requester,
                    ActingEntityId = requester,
                    Purpose = InformationAccessPurpose.Codex,
                    AccessMode = InformationAccessMode.Read,
                    HasDiscoveredSubject = true,
                    RedactedAccessAcceptable = true,
                    RevealDenialReasons = true,
                    WorldTimeSeconds = 10d,
                    DeterministicPolicyId = "information-access.policy.record.secret"
                }
            };
        }

        private sealed class ReadFixture : IDisposable
        {
            private readonly GameObject gameObject;

            private ReadFixture(GameObject gameObject, DefinitionRegistry registry)
            {
                this.gameObject = gameObject;
                Registry = registry;
                Records = new KnowledgeRecordRuntime();
                Records.Configure(registry, "person.owner");
                Sources = new InformationSourceRuntime();
                Sources.Configure(registry, "person.owner");
                Knowledge = gameObject.AddComponent<PersonKnowledgeRuntime>();
                Knowledge.Configure(registry, "person.owner");
                Memory = new PersonMemoryRuntime();
                Memory.Configure("person.owner", registry, null, new[] { "person.owner" });
            }

            public DefinitionRegistry Registry { get; }
            public KnowledgeRecordRuntime Records { get; }
            public InformationSourceRuntime Sources { get; }
            public PersonKnowledgeRuntime Knowledge { get; }
            public PersonMemoryRuntime Memory { get; }

            public static ReadFixture Create(DefinitionRegistry registry = null)
            {
                if (registry == null)
                {
                    KnowledgeRecordRuntime unused = Runtime(out registry);
                }

                return new ReadFixture(new GameObject("Knowledge Record Read Fixture"), registry);
            }

            public KnowledgeRecordReadRequest ReadRequest(string transactionId, string recordId)
            {
                return new KnowledgeRecordReadRequest
                {
                    TransactionId = transactionId,
                    RecordId = recordId,
                    ReaderPersonId = "person.owner",
                    ProjectionContext = PrivilegedContext(),
                    WorldTimeSeconds = 20d,
                    CreateInformationSource = true,
                    CreateKnowledgeEvidence = true,
                    CreateMemory = true,
                    EvidenceStrength = 450,
                    EvidenceCredibility = 550,
                    EvidenceVisibility = KnowledgeVisibility.Public
                };
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
