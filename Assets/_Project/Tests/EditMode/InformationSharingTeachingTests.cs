using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class InformationSharingTeachingTests
    {
        [Test]
        public void TransferDefinition_ValidatesCanonicalContract()
        {
            InformationTransferDefinition definition = TransferDefinition("information-transfer.test.direct", InformationTransferMode.DirectTestimony);
            DefinitionValidationReport report = new DefinitionValidationReport();

            definition.ValidateCatalogDefinition(new Dictionary<string, IGameDefinition>(), report);

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());
        }

        [Test]
        public void TrueFactTransferCreatesRecipientEvidenceAndMemoryWithoutMutatingSender()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.true", SpeciesCapability(), 900);
            long senderRevision = fixture.SenderKnowledge.KnowledgeRevision;
            long senderMemoryRevision = fixture.SenderMemory.MemoryRevision;

            InformationTransferResult result = fixture.Transfers.ExecuteTransfer(fixture.Request("tx.transfer.true"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.RecipientResults.Single().CreatedEvidenceIds.Count, Is.EqualTo(1));
            Assert.That(result.RecipientResults.Single().FormedMemoryIds.Count, Is.EqualTo(1));
            Assert.That(fixture.RecipientKnowledge.TryGetBelief(SpeciesCapability(), out KnowledgeBeliefRecord belief), Is.True);
            Assert.That(belief.State, Is.Not.EqualTo(KnowledgeBeliefState.Unknown));
            Assert.That(belief.State, Is.Not.EqualTo(KnowledgeBeliefState.Invalid));
            Assert.That(belief.Confidence, Is.GreaterThan(0));
            Assert.That(fixture.SenderKnowledge.KnowledgeRevision, Is.EqualTo(senderRevision));
            Assert.That(fixture.SenderMemory.MemoryRevision, Is.EqualTo(senderMemoryRevision));
        }

        [Test]
        public void PreviewTransferHasNoSideEffects()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.preview", SpeciesCapability(), 900);
            long transferRevision = fixture.Transfers.TransferRevision;
            long sourceRevision = fixture.Sources.SourceRevision;
            long recipientRevision = fixture.RecipientKnowledge.KnowledgeRevision;

            InformationTransferResult result = fixture.Transfers.PreviewTransfer(fixture.Request("tx.transfer.preview"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(fixture.Transfers.TransferRevision, Is.EqualTo(transferRevision));
            Assert.That(fixture.Sources.SourceRevision, Is.EqualTo(sourceRevision));
            Assert.That(fixture.RecipientKnowledge.KnowledgeRevision, Is.EqualTo(recipientRevision));
        }

        [Test]
        public void DeliberateFalsehoodRequiresExplicitAuthorization()
        {
            using Fixture fixture = new Fixture();
            InformationTransferRequest blocked = fixture.Request("tx.false.blocked");
            blocked.ContentItems[0].deliberateFalsehood = true;

            InformationTransferResult rejected = fixture.Transfers.ExecuteTransfer(blocked);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Status, Is.EqualTo(InformationTransferStatus.SenderAccessDenied));

            InformationTransferRequest authorized = fixture.Request("tx.false.authorized");
            authorized.ContentItems[0].deliberateFalsehood = true;
            authorized.DeliberateFalsehoodAuthorized = true;

            InformationTransferResult accepted = fixture.Transfers.ExecuteTransfer(authorized);

            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(fixture.RecipientKnowledge.TryGetBelief(SpeciesCapability(), out _), Is.True);
        }

        [Test]
        public void SuppressedMemoryBlocksRecallRequiredTransferWithoutMutation()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.recall", SpeciesCapability(), 900);
            string memoryId = fixture.FormSenderMemory();
            fixture.SenderMemory.AddSuppression(new MemorySuppressionRequest
            {
                TransactionId = "tx.suppression",
                OwnerPersonId = fixture.SenderId,
                MemoryId = memoryId,
                SuppressionId = "suppression.test.transfer",
                SourceId = "test",
                StartedAtWorldTime = 9d,
                AllowsCueBypass = false
            });
            long senderMemoryRevision = fixture.SenderMemory.MemoryRevision;
            InformationTransferRequest request = fixture.Request("tx.transfer.recall", recallRequired: true);
            request.ContentItems[0].senderMemoryId = memoryId;

            InformationTransferResult result = fixture.Transfers.ExecuteTransfer(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(InformationTransferStatus.RecallFailed));
            Assert.That(fixture.SenderMemory.MemoryRevision, Is.EqualTo(senderMemoryRevision));
            Assert.That(fixture.RecipientKnowledge.CreateSnapshot().Evidence.Count, Is.EqualTo(0));
        }

        [Test]
        public void SourceReliabilityChangesInheritedConfidence()
        {
            using Fixture high = new Fixture();
            high.RecordSenderBelief("tx.sender.high", SpeciesCapability(), 900);
            high.RegisterSource("information-source.test.high", InformationSourceCategory.ExpertTestimony, "person.expert");
            high.Assess("person.recipient", "information-source.test.high", 950, 50, 50, 50);
            InformationTransferRequest highRequest = high.Request("tx.high");
            highRequest.ImmediateSourceId = "information-source.test.high";
            InformationTransferResult highResult = high.Transfers.ExecuteTransfer(highRequest);

            using Fixture low = new Fixture();
            low.RecordSenderBelief("tx.sender.low", SpeciesCapability(), 900);
            low.RegisterSource("information-source.test.low", InformationSourceCategory.AnonymousTestimony, "person.unknown");
            low.Assess("person.recipient", "information-source.test.low", 250, 850, 850, 800);
            InformationTransferRequest lowRequest = low.Request("tx.low");
            lowRequest.ImmediateSourceId = "information-source.test.low";
            InformationTransferResult lowResult = low.Transfers.ExecuteTransfer(lowRequest);

            Assert.That(highResult.Succeeded, Is.True, highResult.Message);
            Assert.That(lowResult.Succeeded, Is.True, lowResult.Message);
            Assert.That(highResult.RecipientResults.Single().InheritedConfidence, Is.GreaterThan(lowResult.RecipientResults.Single().InheritedConfidence));
        }

        [Test]
        public void TeachingProcedureCreatesCommunicationRecordWithoutGrantingCapability()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.teach", SpeciesCapability(), 900);
            InformationTransferRequest request = fixture.Request("tx.teach");
            request.Mode = InformationTransferMode.Demonstration;
            request.TransferDefinitionId = "information-transfer.test.demo";
            request.TeachingRequested = true;
            request.ContentItems[0].contentType = InformationTransferContentType.ProcedureReference;
            request.ContentItems[0].typedPayloadId = "procedure.test.first-aid";

            InformationTransferResult result = fixture.Transfers.ExecuteTransfer(request);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Record.Data.teachingRequested, Is.True);
            Assert.That(result.Record.ContentItems.Single().typedPayloadId, Is.EqualTo("procedure.test.first-aid"));
            Assert.That(result.RecipientResults.Single().FormedMemoryIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void TransferSnapshotsAreImmutable()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.snapshot", SpeciesCapability(), 900);
            fixture.Transfers.ExecuteTransfer(fixture.Request("tx.snapshot"));

            InformationTransferSnapshot snapshot = fixture.Transfers.CreateSnapshot();
            snapshot.Transfers[0].Data.transferId = "mutated";
            snapshot.Transfers[0].Data.contentItems[0].contentItemId = "mutated-content";
            snapshot.Transfers[0].Data.recipientResults[0].createdEvidenceIds = new[] { "mutated-evidence" };

            InformationTransferSnapshot fresh = fixture.Transfers.CreateSnapshot();
            Assert.That(fresh.Transfers.Any(transfer => transfer.TransferId == "mutated"), Is.False);
            Assert.That(fresh.Transfers.Any(transfer => transfer.ContentItems.Any(content => content.contentItemId == "mutated-content")), Is.False);
            Assert.That(fresh.Transfers.Any(transfer => transfer.RecipientResults.Any(result => result.CreatedEvidenceIds.Contains("mutated-evidence"))), Is.False);
        }

        [Test]
        public void SaveRestoreRejectsCorruptPayloadWithoutPartialMutation()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.save", SpeciesCapability(), 900);
            fixture.Transfers.ExecuteTransfer(fixture.Request("tx.save"));
            InformationTransferSaveData corrupt = fixture.Transfers.CreateSaveData();
            corrupt.transfers[0].recipientPersonIds = Array.Empty<string>();
            long beforeRevision = fixture.Transfers.TransferRevision;
            int beforeCount = fixture.Transfers.CreateSnapshot().Transfers.Count;

            InformationTransferResult result = fixture.Transfers.RestoreFromSaveData(corrupt, fixture.Registry, fixture.SenderId, restoring: true);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Transfers.TransferRevision, Is.EqualTo(beforeRevision));
            Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Count, Is.EqualTo(beforeCount));
        }

        [Test]
        public void PersistenceParticipantPrepareCommitRestoresAuditSilently()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.persist", SpeciesCapability(), 900);
            fixture.Transfers.ExecuteTransfer(fixture.Request("tx.persist"));
            InformationTransferPersistenceParticipant participant = new InformationTransferPersistenceParticipant(fixture.Transfers, () => fixture.Registry, fixture.SenderId);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(save.PayloadJson, participant.ParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = participant.CommitPreparedPayload(prepare.PreparedPayload);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Count, Is.EqualTo(1));
        }

        [Test]
        public void MultiRecipientFailureRollsBackAllDependentRuntimes()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.multi", SpeciesCapability(), 900);
            GameObject badObject = new GameObject("Bad Recipient");
            try
            {
                PersonKnowledgeRuntime badRecipient = badObject.AddComponent<PersonKnowledgeRuntime>();
                badRecipient.Configure(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "person.second");
                InformationTransferRequest request = fixture.Request("tx.multi");
                request.RecipientPersonIds = new[] { fixture.RecipientId, "person.second" };
                request.RecipientKnowledgeRuntimes = new Dictionary<string, PersonKnowledgeRuntime>
                {
                    [fixture.RecipientId] = fixture.RecipientKnowledge,
                    ["person.second"] = badRecipient
                };
                request.RecipientMemoryRuntimes = new Dictionary<string, PersonMemoryRuntime> { [fixture.RecipientId] = fixture.RecipientMemory };
                int sourceCount = fixture.Sources.CreateSnapshot().Sources.Count;

                InformationTransferResult result = fixture.Transfers.ExecuteTransfer(request);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Status, Is.EqualTo(InformationTransferStatus.KnowledgeRejected));
                Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Count, Is.EqualTo(0));
                Assert.That(fixture.Sources.CreateSnapshot().Sources.Count, Is.EqualTo(sourceCount));
                Assert.That(fixture.RecipientKnowledge.CreateSnapshot().Evidence.Count, Is.EqualTo(0));
                Assert.That(fixture.RecipientMemory.CreateSnapshot().Memories.Count, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(badObject);
            }
        }

        [Test]
        public void CorrectionsAndRetractionsCreateAuditLinksWithoutDeletingOriginals()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.correction", SpeciesCapability(), 900);
            InformationTransferResult original = fixture.Transfers.ExecuteTransfer(fixture.Request("tx.original"));
            int evidenceBefore = fixture.RecipientKnowledge.CreateSnapshot().Evidence.Count;
            int memoryBefore = fixture.RecipientMemory.CreateSnapshot().Memories.Count;

            InformationTransferRequest correction = fixture.Request("tx.correction");
            correction.TransferDefinitionId = string.Empty;
            correction.Mode = InformationTransferMode.Explanation;
            correction.CorrectionOfTransferId = original.Record.TransferId;
            correction.ImmediateSourceId = original.Record.Data.createdSourceId;
            correction.ContentItems[0].assertionType = InformationTransferAssertionType.Correction;
            InformationTransferResult corrected = fixture.Transfers.ExecuteTransfer(correction);

            InformationTransferRequest retraction = fixture.Request("tx.retraction");
            retraction.TransferDefinitionId = string.Empty;
            retraction.Mode = InformationTransferMode.Explanation;
            retraction.RetractionOfTransferId = original.Record.TransferId;
            retraction.ImmediateSourceId = original.Record.Data.createdSourceId;
            retraction.ContentItems[0].assertionType = InformationTransferAssertionType.Retraction;
            InformationTransferResult retracted = fixture.Transfers.ExecuteTransfer(retraction);

            Assert.That(original.Succeeded, Is.True, original.Message);
            Assert.That(corrected.Succeeded, Is.True, corrected.Message);
            Assert.That(retracted.Succeeded, Is.True, retracted.Message);
            Assert.That(corrected.Record.Data.correctionOfTransferId, Is.EqualTo(original.Record.TransferId));
            Assert.That(retracted.Record.Data.retractionOfTransferId, Is.EqualTo(original.Record.TransferId));
            Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Any(transfer => transfer.TransferId == original.Record.TransferId), Is.True);
            Assert.That(fixture.RecipientKnowledge.CreateSnapshot().Evidence.Count, Is.GreaterThan(evidenceBefore));
            Assert.That(fixture.RecipientMemory.CreateSnapshot().Memories.Count, Is.GreaterThan(memoryBefore));
        }

        [Test]
        public void RejectedCorrectionLeavesOriginalAuditableAndUnchanged()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.rejected-correction", SpeciesCapability(), 900);
            InformationTransferResult original = fixture.Transfers.ExecuteTransfer(fixture.Request("tx.rejected-correction.original"));
            InformationTransferSaveData beforeSave = fixture.Transfers.CreateSaveData();
            int sourceCount = fixture.Sources.CreateSnapshot().Sources.Count;
            GameObject badObject = new GameObject("Correction Rejecting Recipient");
            try
            {
                PersonKnowledgeRuntime badRecipient = badObject.AddComponent<PersonKnowledgeRuntime>();
                badRecipient.Configure(new DefinitionRegistry(Array.Empty<IGameDefinition>()), fixture.RecipientId);
                InformationTransferRequest correction = fixture.Request("tx.rejected-correction.followup");
                correction.TransferDefinitionId = string.Empty;
                correction.Mode = InformationTransferMode.Explanation;
                correction.CorrectionOfTransferId = original.Record.TransferId;
                correction.ImmediateSourceId = original.Record.Data.createdSourceId;
                correction.ContentItems[0].assertionType = InformationTransferAssertionType.Correction;
                correction.RecipientKnowledgeRuntimes = new Dictionary<string, PersonKnowledgeRuntime> { [fixture.RecipientId] = badRecipient };

                InformationTransferResult rejected = fixture.Transfers.ExecuteTransfer(correction);

                Assert.That(rejected.Succeeded, Is.False);
                Assert.That(rejected.Status, Is.EqualTo(InformationTransferStatus.KnowledgeRejected));
                Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Select(transfer => transfer.TransferId), Is.EquivalentTo(beforeSave.transfers.Select(transfer => transfer.transferId)));
                Assert.That(fixture.Sources.CreateSnapshot().Sources.Count, Is.EqualTo(sourceCount));
                Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Any(transfer => transfer.TransferId == original.Record.TransferId), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(badObject);
            }
        }

        [Test]
        public void SelfCorrectionAndCircularSavedChainsAreRejected()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.self", SpeciesCapability(), 900);
            InformationTransferRequest self = fixture.Request("tx.self");
            self.TransferId = "transfer.self";
            self.CorrectionOfTransferId = "transfer.self";

            InformationTransferResult selfResult = fixture.Transfers.ExecuteTransfer(self);

            Assert.That(selfResult.Succeeded, Is.False);
            Assert.That(selfResult.Status, Is.EqualTo(InformationTransferStatus.CircularChain));

            InformationTransferSaveData saveData = new InformationTransferSaveData
            {
                schemaVersion = InformationTransferSaveData.CurrentSchemaVersion,
                ownerId = fixture.SenderId,
                transferRevision = 2,
                transfers = new[]
                {
                    SavedTransfer("transfer.a", fixture.SenderId, fixture.RecipientId, parent: "transfer.b"),
                    SavedTransfer("transfer.b", fixture.SenderId, fixture.RecipientId, parent: "transfer.a")
                },
                processedTransactions = Array.Empty<InformationTransferProcessedTransactionData>()
            };

            Assert.That(InformationTransferRuntime.ValidateSaveData(saveData, fixture.Registry, fixture.SenderId, out string failure), Is.False);
            Assert.That(failure, Does.Contain("circular"));
        }

        [Test]
        public void ClarificationLinksToOriginalAndSurvivesSaveRestore()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.clarify", SpeciesCapability(), 900);
            InformationTransferRequest partial = fixture.Request("tx.partial");
            partial.TransferDefinitionId = string.Empty;
            partial.SummarizationRequested = true;
            partial.OmissionRequested = true;
            partial.ContentItems[0].deliberateOmission = true;
            partial.ContentItems[0].omittedDetailIds = new[] { "detail.location" };
            InformationTransferResult original = fixture.Transfers.ExecuteTransfer(partial);

            InformationTransferRequest clarification = fixture.Request("tx.clarify");
            clarification.TransferDefinitionId = string.Empty;
            clarification.Mode = InformationTransferMode.Explanation;
            clarification.ParentTransferId = original.Record.TransferId;
            clarification.ImmediateSourceId = original.Record.Data.createdSourceId;
            clarification.ContentItems[0].includedDetailIds = new[] { "detail.location" };
            InformationTransferResult clarified = fixture.Transfers.ExecuteTransfer(clarification);

            InformationTransferSaveData saveData = fixture.Transfers.CreateSaveData();
            InformationTransferRuntime restored = new InformationTransferRuntime();
            restored.Configure(fixture.Registry, fixture.SenderId);
            InformationTransferResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, fixture.SenderId, restoring: true);

            Assert.That(original.Succeeded, Is.True, original.Message);
            Assert.That(clarified.Succeeded, Is.True, clarified.Message);
            Assert.That(original.RecipientResults.Single().Data.omittedContentItemIds.Count, Is.GreaterThanOrEqualTo(0));
            Assert.That(clarified.Record.Data.parentTransferId, Is.EqualTo(original.Record.TransferId));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.CreateSnapshot().Transfers.Single(transfer => transfer.TransferId == clarified.Record.TransferId).Data.parentTransferId, Is.EqualTo(original.Record.TransferId));
        }

        [Test]
        public void ResharingCreatesDependentSourceAndPreservesDistortion()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.reshare", SpeciesCapability(), 900);
            InformationTransferResult original = fixture.Transfers.ExecuteTransfer(fixture.Request("tx.reshare.original"));

            InformationTransferRequest reshare = fixture.Request("tx.reshare.copy");
            reshare.TransferDefinitionId = string.Empty;
            reshare.Mode = InformationTransferMode.RumorRetelling;
            reshare.ParentTransferId = original.Record.TransferId;
            reshare.ImmediateSourceId = original.Record.Data.createdSourceId;
            reshare.DistortionRequested = true;
            reshare.ContentItems[0].deliberateDistortion = true;
            InformationTransferResult reshared = fixture.Transfers.ExecuteTransfer(reshare);

            InformationTransferRequest second = fixture.Request("tx.reshare.second");
            second.TransferDefinitionId = string.Empty;
            second.Mode = InformationTransferMode.Summary;
            second.ParentTransferId = original.Record.TransferId;
            second.ImmediateSourceId = original.Record.Data.createdSourceId;
            InformationTransferResult resharedAgain = fixture.Transfers.ExecuteTransfer(second);

            Assert.That(reshared.Succeeded, Is.True, reshared.Message);
            Assert.That(resharedAgain.Succeeded, Is.True, resharedAgain.Message);
            Assert.That(reshared.Record.Data.createdSourceId, Is.Not.EqualTo(original.Record.Data.createdSourceId));
            Assert.That(fixture.Sources.CompareIndependence(original.Record.Data.createdSourceId, reshared.Record.Data.createdSourceId), Is.EqualTo(SourceIndependenceState.Dependent));
            Assert.That(fixture.Sources.CompareIndependence(reshared.Record.Data.createdSourceId, resharedAgain.Record.Data.createdSourceId), Is.EqualTo(SourceIndependenceState.Dependent));
            Assert.That(reshared.RecipientResults.Single().Understanding, Is.EqualTo(TransferUnderstandingState.Misinterpreted));
            Assert.That(reshared.Record.Data.distortionRequested, Is.True);
        }

        [Test]
        public void SenderRecallPreviewDoesNotMutateRecallMetadata()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.recall-preview", SpeciesCapability(), 900);
            string memoryId = fixture.FormSenderMemory();
            InformationTransferRequest request = fixture.Request("tx.recall.preview", recallRequired: true);
            request.ContentItems[0].senderMemoryId = memoryId;
            HistoryMemoryRecord before = fixture.SenderMemory.CreateSnapshot().Memories.Single(memory => memory.MemoryId == memoryId);

            InformationTransferResult result = fixture.Transfers.ExecuteTransfer(request);

            HistoryMemoryRecord after = fixture.SenderMemory.CreateSnapshot().Memories.Single(memory => memory.MemoryId == memoryId);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(after.RecallCount, Is.EqualTo(before.RecallCount));
            Assert.That(after.LastRecalledWorldTime, Is.EqualTo(before.LastRecalledWorldTime));
            Assert.That(after.LastRecallAttemptWorldTime, Is.EqualTo(before.LastRecallAttemptWorldTime));
            Assert.That(fixture.SenderMemory.MemoryRevision, Is.EqualTo(fixture.SenderMemory.CreateSnapshot().Revision));
        }

        [Test]
        public void RecipientUnderstandingAndAcceptanceStatesRemainDistinct()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.states", SpeciesCapability(), 900);
            InformationTransferRequest accepted = fixture.Request("tx.accepted");
            InformationTransferResult acceptedResult = fixture.Transfers.ExecuteTransfer(accepted);

            InformationTransferRequest uncertain = fixture.Request("tx.uncertain");
            uncertain.SummarizationRequested = true;
            uncertain.ContentItems[0].rawEvidenceStrength = 420;
            InformationTransferResult uncertainResult = fixture.Transfers.ExecuteTransfer(uncertain);

            InformationTransferRequest misunderstood = fixture.Request("tx.misunderstood");
            misunderstood.DistortionRequested = true;
            InformationTransferResult misunderstoodResult = fixture.Transfers.ExecuteTransfer(misunderstood);

            InformationTransferRequest sourceOnly = fixture.Request("tx.source-only");
            sourceOnly.ContentItems[0].proposition = null;
            sourceOnly.ContentItems[0].contentType = InformationTransferContentType.SourceIdentity;
            InformationTransferResult sourceOnlyResult = fixture.Transfers.ExecuteTransfer(sourceOnly);

            Assert.That(acceptedResult.RecipientResults.Single().Understanding, Is.EqualTo(TransferUnderstandingState.Complete));
            Assert.That(uncertainResult.RecipientResults.Single().Understanding, Is.EqualTo(TransferUnderstandingState.Partial));
            Assert.That(misunderstoodResult.RecipientResults.Single().Understanding, Is.EqualTo(TransferUnderstandingState.Misinterpreted));
            Assert.That(sourceOnlyResult.Succeeded, Is.True, sourceOnlyResult.Message);
            Assert.That(sourceOnlyResult.RecipientResults.Single().CreatedEvidenceIds.Count, Is.EqualTo(0));
            Assert.That(sourceOnlyResult.RecipientResults.Single().FormedMemoryIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestoreRejectsDuplicateMissingAndSelfReferencesWithoutMutation()
        {
            using Fixture fixture = new Fixture();
            fixture.RecordSenderBelief("tx.sender.restore", SpeciesCapability(), 900);
            fixture.Transfers.ExecuteTransfer(fixture.Request("tx.restore.good"));
            long beforeRevision = fixture.Transfers.TransferRevision;
            int beforeCount = fixture.Transfers.CreateSnapshot().Transfers.Count;

            InformationTransferSaveData duplicate = fixture.Transfers.CreateSaveData();
            duplicate.transfers = duplicate.transfers.Concat(new[] { duplicate.transfers[0].Clone() }).ToArray();
            InformationTransferResult duplicateResult = fixture.Transfers.RestoreFromSaveData(duplicate, fixture.Registry, fixture.SenderId, restoring: true);

            InformationTransferSaveData missing = fixture.Transfers.CreateSaveData();
            missing.transfers[0].parentTransferId = "transfer.missing";
            InformationTransferResult missingResult = fixture.Transfers.RestoreFromSaveData(missing, fixture.Registry, fixture.SenderId, restoring: true);

            InformationTransferSaveData self = fixture.Transfers.CreateSaveData();
            self.transfers[0].correctionOfTransferId = self.transfers[0].transferId;
            InformationTransferResult selfResult = fixture.Transfers.RestoreFromSaveData(self, fixture.Registry, fixture.SenderId, restoring: true);

            Assert.That(duplicateResult.Succeeded, Is.False);
            Assert.That(missingResult.Succeeded, Is.False);
            Assert.That(selfResult.Succeeded, Is.False);
            Assert.That(fixture.Transfers.TransferRevision, Is.EqualTo(beforeRevision));
            Assert.That(fixture.Transfers.CreateSnapshot().Transfers.Count, Is.EqualTo(beforeCount));
        }

        private static KnowledgePropositionData SpeciesCapability()
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                subjectType = KnowledgeSubjectType.Species,
                subjectId = "species.test",
                valueType = KnowledgeValueType.StableId,
                stableValueId = "capability.can.test"
            };
        }

        private static KnowledgeFactDefinition Fact()
        {
            KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            Set(definition, "factId", BuiltInKnowledgeFacts.SpeciesCapability);
            Set(definition, "displayName", "Species Capability");
            Set(definition, "domain", KnowledgeDomain.Species);
            Set(definition, "propositionType", KnowledgePropositionType.Capability);
            Set(definition, "subjectType", KnowledgeSubjectType.Species);
            Set(definition, "valueType", KnowledgeValueType.StableId);
            Set(definition, "defaultVisibility", KnowledgeVisibility.Public);
            Set(definition, "certaintyThreshold", 700);
            return definition;
        }

        private static InformationTransferDefinition TransferDefinition(string id, InformationTransferMode mode)
        {
            InformationTransferDefinition definition = ScriptableObject.CreateInstance<InformationTransferDefinition>();
            definition.DevelopmentConfigure(
                id,
                id,
                mode,
                new[] { KnowledgeDomain.Species, KnowledgeDomain.Medical },
                Enum.GetValues(typeof(InformationSourceCategory))
                    .Cast<InformationSourceCategory>()
                    .Where(category => category != InformationSourceCategory.Unknown)
                    .ToArray(),
                false,
                true,
                true,
                mode == InformationTransferMode.Demonstration,
                850,
                850,
                TransferMemoryPolicy.FormCommunicationMemory,
                TransferEvidencePolicy.CreateRecipientEvidence);
            return definition;
        }

        private static void Set<T>(KnowledgeFactDefinition definition, string fieldName, T value)
        {
            FieldInfo field = typeof(KnowledgeFactDefinition).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(definition, value);
        }

        private static InformationTransferRecordData SavedTransfer(string transferId, string senderId, string recipientId, string parent = "", string correction = "", string retraction = "")
        {
            return new InformationTransferRecordData
            {
                transferId = transferId,
                transactionId = $"tx.{transferId}",
                senderPersonId = senderId,
                recipientPersonIds = new[] { recipientId },
                mode = InformationTransferMode.DirectTestimony,
                parentTransferId = parent,
                correctionOfTransferId = correction,
                retractionOfTransferId = retraction,
                contentItems = new[]
                {
                    new TransferContentItemData
                    {
                        contentItemId = $"content.{transferId}",
                        contentType = InformationTransferContentType.BeliefStatement,
                        proposition = SpeciesCapability()
                    }
                },
                recipientResults = new[]
                {
                    new TransferRecipientResultData
                    {
                        recipientPersonId = recipientId,
                        status = InformationTransferStatus.Succeeded,
                        understanding = TransferUnderstandingState.Complete
                    }
                }
            };
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject senderObject = new GameObject("Transfer Sender");
            private readonly GameObject recipientObject = new GameObject("Transfer Recipient");
            private readonly AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();

            public Fixture()
            {
                SenderId = "person.sender";
                RecipientId = "person.recipient";
                Registry = KnowledgeTestDefinitionFactory.AddSourceDefinitions(new IGameDefinition[]
                {
                    Fact(),
                    TransferDefinition("information-transfer.test.direct", InformationTransferMode.DirectTestimony),
                    TransferDefinition("information-transfer.test.demo", InformationTransferMode.Demonstration),
                    TransferDefinition("information-transfer.test.explanation", InformationTransferMode.Explanation),
                    TransferDefinition("information-transfer.test.summary", InformationTransferMode.Summary),
                    TransferDefinition("information-transfer.test.rumor-retelling", InformationTransferMode.RumorRetelling)
                });
                history.Configure(Registry, "world.test", new[] { SenderId, RecipientId });
                SenderKnowledge = senderObject.AddComponent<PersonKnowledgeRuntime>();
                RecipientKnowledge = recipientObject.AddComponent<PersonKnowledgeRuntime>();
                SenderKnowledge.Configure(Registry, SenderId);
                RecipientKnowledge.Configure(Registry, RecipientId);
                SenderMemory = new PersonMemoryRuntime();
                RecipientMemory = new PersonMemoryRuntime();
                SenderMemory.Configure(SenderId, Registry, history, new[] { SenderId, RecipientId });
                RecipientMemory.Configure(RecipientId, Registry, history, new[] { SenderId, RecipientId });
                Sources = new InformationSourceRuntime();
                Sources.Configure(Registry, SenderId);
                RegisterSource("information-source.test.sender", InformationSourceCategory.PersonalTestimony, SenderId);
                Transfers = new InformationTransferRuntime();
                Transfers.Configure(Registry, SenderId);
            }

            public string SenderId { get; }
            public string RecipientId { get; }
            public DefinitionRegistry Registry { get; }
            public PersonKnowledgeRuntime SenderKnowledge { get; }
            public PersonKnowledgeRuntime RecipientKnowledge { get; }
            public PersonMemoryRuntime SenderMemory { get; }
            public PersonMemoryRuntime RecipientMemory { get; }
            public InformationSourceRuntime Sources { get; }
            public InformationTransferRuntime Transfers { get; }

            public void RecordSenderBelief(string transactionId, KnowledgePropositionData proposition, int strength)
            {
                KnowledgeOperationResult result = SenderKnowledge.RecordObservation(new KnowledgeObservationRequest
                {
                    PersonId = SenderId,
                    TransactionId = transactionId,
                    Proposition = proposition,
                    AcquisitionSource = KnowledgeAcquisitionSource.DirectObservation,
                    Provenance = KnowledgeProvenance.DirectObservation,
                    Direction = KnowledgeEvidenceDirection.Supports,
                    Strength = strength,
                    Credibility = strength,
                    SourceId = "test",
                    InformationSourceId = "information-source.test.sender"
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public string FormSenderMemory()
            {
                string memoryId = $"memory.test.{Guid.NewGuid():N}";
                HistoryOperationResult result = SenderMemory.FormMemory(new FormMemoryRequest
                {
                    TransactionId = $"tx.memory.{Guid.NewGuid():N}",
                    MemoryId = memoryId,
                    OwnerPersonId = SenderId,
                    Source = HistoryMemorySource.DirectObservation,
                    FormedAtWorldTime = 5d,
                    RememberedOccurredAtWorldTime = 4d,
                    Confidence = 800,
                    Clarity = 800,
                    Salience = 600,
                    FirstHand = true,
                    Visibility = KnowledgeVisibility.Private
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
                return memoryId;
            }

            public InformationTransferRequest Request(string transactionId, bool recallRequired = false)
            {
                return new InformationTransferRequest
                {
                    TransactionId = transactionId,
                    TransferId = $"transfer.{transactionId}",
                    SenderPersonId = SenderId,
                    RecipientPersonIds = new[] { RecipientId },
                    TransferDefinitionId = "information-transfer.test.direct",
                    Mode = InformationTransferMode.DirectTestimony,
                    ContentItems = new[]
                    {
                        new TransferContentItemData
                        {
                            contentItemId = "content.test",
                            contentType = InformationTransferContentType.BeliefStatement,
                            domain = KnowledgeDomain.Species,
                            proposition = SpeciesCapability(),
                            senderConfidence = 900,
                            senderBeliefState = KnowledgeBeliefState.Known,
                            privacyClassification = KnowledgeVisibility.Public,
                            assertionType = InformationTransferAssertionType.Fact,
                            rawEvidenceStrength = 800
                        }
                    },
                    ImmediateSourceId = "information-source.test.sender",
                    OriginalSourceId = "information-source.test.sender",
                    WorldTimeSeconds = 10d,
                    SenderRecallRequired = recallRequired,
                    SenderKnowledge = SenderKnowledge,
                    SenderMemory = SenderMemory,
                    SourceRuntime = Sources,
                    RecipientKnowledgeRuntimes = new Dictionary<string, PersonKnowledgeRuntime> { [RecipientId] = RecipientKnowledge },
                    RecipientMemoryRuntimes = new Dictionary<string, PersonMemoryRuntime> { [RecipientId] = RecipientMemory },
                    PrivilegedAccess = true
                };
            }

            public void RegisterSource(string sourceId, InformationSourceCategory category, string referencedId)
            {
                if (Sources.TryGetSource(sourceId, out _))
                {
                    return;
                }

                InformationSourceOperationResult result = Sources.RegisterSource(new InformationSourceRegistrationRequest
                {
                    TransactionId = $"tx.source.{sourceId}",
                    SourceInstanceId = sourceId,
                    Category = category,
                    ReferenceType = InformationSourceReferenceType.Person,
                    ReferencedId = referencedId,
                    OriginalCreatorPersonId = referencedId,
                    HolderPersonId = SenderId,
                    TransmitterPersonId = referencedId,
                    CreationWorldTimeSeconds = 1d,
                    ObservationWorldTimeSeconds = 1d,
                    TransmissionWorldTimeSeconds = 1d,
                    Domain = KnowledgeDomain.Species,
                    SubjectId = "species.test",
                    MethodId = "method.test"
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void Assess(string personId, string sourceId, int dependability, int errorRisk, int deceptionRisk, int biasRisk)
            {
                ReliabilityProfileData reliability = ReliabilityProfileData.Default();
                reliability.generalDependability = dependability;
                reliability.domainExpertise = dependability;
                reliability.methodQuality = dependability;
                reliability.authenticity = dependability;
                reliability.identityCertainty = dependability;
                reliability.observationQuality = dependability;
                reliability.recordIntegrity = dependability;
                reliability.errorRisk = errorRisk;
                reliability.deceptionRisk = deceptionRisk;
                reliability.biasRisk = biasRisk;
                reliability.completeness = dependability;
                reliability.precision = dependability;
                reliability.contextFit = dependability;

                InformationSourceOperationResult result = Sources.AssessSource(new SourceAssessmentRequest
                {
                    TransactionId = $"tx.assess.{sourceId}.{personId}",
                    AssessmentId = $"assessment.{sourceId}.{personId}",
                    AssessingPersonId = personId,
                    SourceInstanceId = sourceId,
                    Domain = KnowledgeDomain.Species,
                    SubjectId = "species.test",
                    MethodId = "method.test",
                    WorldTimeSeconds = 10d,
                    Reliability = reliability,
                    Authority = dependability,
                    ErrorRisk = errorRisk,
                    DeceptionRisk = deceptionRisk,
                    BiasRisk = biasRisk,
                    ConfidenceInAssessment = 900
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(senderObject);
                UnityEngine.Object.DestroyImmediate(recipientObject);
            }
        }
    }
}
