using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Knowledge.Sources
{
    public sealed class InformationSourceRuntime
    {
        private readonly Dictionary<string, InformationSourceInstanceData> sourcesById = new Dictionary<string, InformationSourceInstanceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonSourceAssessmentData> assessmentsById = new Dictionary<string, PersonSourceAssessmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InformationSourceProcessedTransactionData> processedTransactions = new Dictionary<string, InformationSourceProcessedTransactionData>(StringComparer.Ordinal);
        private readonly List<SourceTransformationData> transformations = new List<SourceTransformationData>();
        private DefinitionRegistry registry;
        private string ownerId;
        private bool suppressEvents;

        public event Action<InformationSourceRuntime, InformationSourceOperationResult> SourcesChanged;

        public string OwnerId => ownerId ?? string.Empty;
        public long SourceRevision { get; private set; }

        public void Configure(DefinitionRegistry definitionRegistry, string owner)
        {
            registry = definitionRegistry ?? registry;
            ownerId = owner ?? string.Empty;
        }

        public InformationSourceOperationResult RegisterSource(InformationSourceRegistrationRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = SourceRevision;
            if (!ValidateRegistrationRequest(request, out string failure))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, failure, request?.TransactionId, preview, SourceRevision);
            }

            if (!preview && processedTransactions.TryGetValue(TransactionKey(request.TransactionId), out InformationSourceProcessedTransactionData processed))
            {
                return DuplicateResult(processed, request.TransactionId);
            }

            InformationSourceDefinition definition = ResolveDefinition(request.SourceDefinitionId);
            if (definition == null)
            {
                definition = ResolveDefinition(request.Category);
            }

            if (definition == null)
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.MissingDefinition, $"No authored Information Source definition resolves category '{request.Category}'.", request.TransactionId, preview, SourceRevision);
            }

            if (definition.Category != request.Category)
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Information Source definition '{definition.Id}' declares category '{definition.Category}', not '{request.Category}'.", request.TransactionId, preview, SourceRevision);
            }

            if (definition.SupportedDomains.Count > 0 && request.Domain != KnowledgeDomain.Unknown && !definition.SupportedDomains.Contains(request.Domain))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Information Source definition '{definition.Id}' does not support domain '{request.Domain}'.", request.TransactionId, preview, SourceRevision);
            }

            if (definition.SupportedMethodIds.Count > 0 && !definition.SupportedMethodIds.Contains(request.MethodId ?? string.Empty, StringComparer.Ordinal))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Information Source definition '{definition.Id}' does not support method '{request.MethodId}'.", request.TransactionId, preview, SourceRevision);
            }

            if (string.IsNullOrWhiteSpace(request.OriginalCreatorPersonId) && !definition.AllowsAnonymous)
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Information Source definition '{definition.Id}' does not allow anonymous sources.", request.TransactionId, preview, SourceRevision);
            }
            string sourceId = string.IsNullOrWhiteSpace(request.SourceInstanceId)
                ? StableSourceId(request.Category, request.ReferencedId, request.TransactionId)
                : request.SourceInstanceId.Trim();
            if (!preview && sourcesById.TryGetValue(sourceId, out InformationSourceInstanceData existing))
            {
                if (existing.category != request.Category || !string.Equals(existing.referencedId, request.ReferencedId ?? string.Empty, StringComparison.Ordinal))
                {
                    return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Source instance '{sourceId}' already exists with different identity.", request.TransactionId, preview, SourceRevision);
                }

                RememberTransaction(request.TransactionId, InformationSourceResultCode.Duplicate, sourceId, string.Empty);
                return InformationSourceOperationResult.Success("Duplicate source registration ignored.", request.TransactionId, new InformationSourceRecord(existing), null, SourceRevision, SourceRevision, duplicate: true);
            }

            InformationSourceInstanceData data = new InformationSourceInstanceData
            {
                sourceInstanceId = sourceId,
                sourceDefinitionId = definition.Id,
                category = request.Category,
                referenceType = request.ReferenceType,
                referencedId = request.ReferencedId ?? string.Empty,
                originalCreatorPersonId = request.OriginalCreatorPersonId ?? string.Empty,
                observerPersonId = request.ObserverPersonId ?? string.Empty,
                holderPersonId = request.HolderPersonId ?? string.Empty,
                transmitterPersonId = request.TransmitterPersonId ?? string.Empty,
                creationWorldTimeSeconds = Math.Max(0d, request.CreationWorldTimeSeconds),
                observationWorldTimeSeconds = Math.Max(0d, request.ObservationWorldTimeSeconds),
                transmissionWorldTimeSeconds = Math.Max(0d, request.TransmissionWorldTimeSeconds),
                generation = 0,
                parentSourceId = string.Empty,
                originalSourceId = sourceId,
                verificationState = definition != null && definition.RequiresIdentityVerification ? SourceVerificationState.Unverified : SourceVerificationState.Verified,
                authenticityState = SourceVerificationState.Unverified,
                domain = request.Domain,
                subjectId = request.SubjectId ?? string.Empty,
                methodId = request.MethodId ?? string.Empty,
                authorityClassification = request.AuthorityClassification ?? string.Empty,
                biasProfileId = request.BiasProfileId ?? string.Empty,
                errorRisk = KnowledgeConfidence.Clamp(request.ErrorRisk),
                deceptionRisk = KnowledgeConfidence.Clamp(request.DeceptionRisk),
                biasRisk = KnowledgeConfidence.Clamp(request.BiasRisk),
                privacy = request.Privacy,
                tags = request.Tags ?? Array.Empty<string>(),
                revision = 1L
            };

            InformationSourceRecord record = new InformationSourceRecord(data);
            if (preview)
            {
                return InformationSourceOperationResult.Success("Source registration preview resolved without mutation.", request.TransactionId, record, null, priorRevision, priorRevision, preview: true);
            }

            sourcesById[sourceId] = data.Clone();
            SourceRevision++;
            RememberTransaction(request.TransactionId, InformationSourceResultCode.Success, sourceId, string.Empty);
            InformationSourceOperationResult result = InformationSourceOperationResult.Success("Source registered.", request.TransactionId, record, null, priorRevision, SourceRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public InformationSourceOperationResult TransformSource(SourceTransformationRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = SourceRevision;
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.ParentSourceId) || string.IsNullOrWhiteSpace(request.SourceInstanceId))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, "Source transformation requires transaction, parent, and new source IDs.", request?.TransactionId, preview, SourceRevision);
            }

            if (!preview && processedTransactions.TryGetValue(TransactionKey(request.TransactionId), out InformationSourceProcessedTransactionData processed))
            {
                return DuplicateResult(processed, request.TransactionId);
            }

            if (!sourcesById.TryGetValue(request.ParentSourceId, out InformationSourceInstanceData parent))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.MissingSource, $"Parent source '{request.ParentSourceId}' is missing.", request.TransactionId, preview, SourceRevision);
            }

            InformationSourceDefinition parentDefinition = ResolveDefinition(parent.sourceDefinitionId);
            bool transformationAllowed = request.TransformationType switch
            {
                InformationSourceTransformationType.Copy => parentDefinition != null && parentDefinition.AllowsCopying,
                InformationSourceTransformationType.Translation => parentDefinition != null && parentDefinition.AllowsTranslation,
                InformationSourceTransformationType.Summary => parentDefinition != null && parentDefinition.AllowsSummary,
                InformationSourceTransformationType.Correction or InformationSourceTransformationType.Supersession or InformationSourceTransformationType.Inference => parentDefinition != null,
                _ => false
            };
            if (!transformationAllowed)
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidRequest, $"Information Source definition '{parent.sourceDefinitionId}' does not allow transformation '{request.TransformationType}'.", request.TransactionId, preview, SourceRevision);
            }

            InformationSourceCategory category = request.TransformationType switch
            {
                InformationSourceTransformationType.Copy => InformationSourceCategory.CopiedSource,
                InformationSourceTransformationType.Translation => InformationSourceCategory.Translation,
                InformationSourceTransformationType.Summary => InformationSourceCategory.Summary,
                _ => InformationSourceCategory.Custom
            };
            InformationSourceDefinition childDefinition = ResolveDefinition(category);
            if (childDefinition == null)
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.MissingDefinition, $"No authored Information Source definition resolves transformed category '{category}'.", request.TransactionId, preview, SourceRevision);
            }

            InformationSourceInstanceData child = parent.Clone();
            child.sourceInstanceId = request.SourceInstanceId.Trim();
            child.category = category;
            child.sourceDefinitionId = childDefinition.Id;
            child.parentSourceId = parent.sourceInstanceId;
            child.originalSourceId = string.IsNullOrWhiteSpace(parent.originalSourceId) ? parent.sourceInstanceId : parent.originalSourceId;
            child.transmitterPersonId = request.ActorPersonId ?? string.Empty;
            child.transmissionWorldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds);
            child.generation = Math.Max(1, parent.generation + 1);
            child.hidesOriginal = request.HidesOriginal;
            child.revision = 1L;

            SourceTransformationData transformation = new SourceTransformationData
            {
                transformationId = $"source-transformation.{request.TransactionId}",
                transformationType = request.TransformationType,
                fromSourceId = parent.sourceInstanceId,
                toSourceId = child.sourceInstanceId,
                actorPersonId = request.ActorPersonId ?? string.Empty,
                worldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                quality = request.Quality,
                note = request.Note ?? string.Empty
            };

            if (preview)
            {
                return InformationSourceOperationResult.Success("Source transformation preview resolved without mutation.", request.TransactionId, new InformationSourceRecord(child), null, priorRevision, priorRevision, preview: true);
            }

            sourcesById[child.sourceInstanceId] = child.Clone();
            transformations.Add(transformation.Clone());
            SourceRevision++;
            RememberTransaction(request.TransactionId, InformationSourceResultCode.Success, child.sourceInstanceId, string.Empty);
            InformationSourceOperationResult result = InformationSourceOperationResult.Success("Source transformation registered.", request.TransactionId, new InformationSourceRecord(child), null, priorRevision, SourceRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public InformationSourceOperationResult AssessSource(SourceAssessmentRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = SourceRevision;
            if (!ValidateAssessmentRequest(request, out string failure))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.InvalidAssessment, failure, request?.TransactionId, preview, SourceRevision);
            }

            if (!preview && processedTransactions.TryGetValue(TransactionKey(request.TransactionId), out InformationSourceProcessedTransactionData processed))
            {
                return DuplicateResult(processed, request.TransactionId);
            }

            if (!sourcesById.ContainsKey(request.SourceInstanceId))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.MissingSource, $"Source '{request.SourceInstanceId}' is missing.", request.TransactionId, preview, SourceRevision);
            }

            string assessmentId = string.IsNullOrWhiteSpace(request.AssessmentId)
                ? StableAssessmentId(request.AssessingPersonId, request.SourceInstanceId, request.Domain, request.SubjectId)
                : request.AssessmentId.Trim();
            PersonSourceAssessmentData prior = assessmentsById.TryGetValue(assessmentId, out PersonSourceAssessmentData existing) ? existing.Clone() : null;
            PersonSourceAssessmentData data = new PersonSourceAssessmentData
            {
                assessmentId = assessmentId,
                assessingPersonId = request.AssessingPersonId ?? string.Empty,
                sourceInstanceId = request.SourceInstanceId ?? string.Empty,
                domain = request.Domain,
                subjectId = request.SubjectId ?? string.Empty,
                methodId = request.MethodId ?? string.Empty,
                assessmentWorldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                reliability = request.Reliability?.Clone() ?? ReliabilityProfileData.Default(),
                authority = KnowledgeConfidence.Clamp(request.Authority),
                errorRisk = KnowledgeConfidence.Clamp(request.ErrorRisk),
                deceptionRisk = KnowledgeConfidence.Clamp(request.DeceptionRisk),
                biasRisk = KnowledgeConfidence.Clamp(request.BiasRisk),
                familiarity = KnowledgeConfidence.Clamp(request.Familiarity),
                confidenceInAssessment = KnowledgeConfidence.Clamp(request.ConfidenceInAssessment),
                supportingEvidenceIds = request.SupportingEvidenceIds ?? Array.Empty<string>(),
                priorExperienceIds = request.PriorExperienceIds ?? Array.Empty<string>(),
                supersedesAssessmentId = prior?.assessmentId ?? string.Empty,
                privacy = request.Privacy,
                revision = (prior?.revision ?? 0L) + 1L
            };

            PersonSourceAssessmentRecord record = new PersonSourceAssessmentRecord(data);
            if (preview)
            {
                return InformationSourceOperationResult.Success("Source assessment preview resolved without mutation.", request.TransactionId, null, record, priorRevision, priorRevision, preview: true);
            }

            assessmentsById[assessmentId] = data.Clone();
            SourceRevision++;
            RememberTransaction(request.TransactionId, InformationSourceResultCode.Success, request.SourceInstanceId, assessmentId);
            InformationSourceOperationResult result = InformationSourceOperationResult.Success("Source assessment recorded.", request.TransactionId, new InformationSourceRecord(sourcesById[request.SourceInstanceId]), record, priorRevision, SourceRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public SourceReliabilityResult EvaluateReliability(SourceReliabilityRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceInstanceId))
            {
                return ReliabilityFailure(InformationSourceResultCode.InvalidRequest, "Reliability evaluation requires a source ID.", request);
            }

            if (!sourcesById.TryGetValue(request.SourceInstanceId, out InformationSourceInstanceData source))
            {
                return ReliabilityFailure(InformationSourceResultCode.MissingSource, $"Source '{request.SourceInstanceId}' is missing.", request);
            }

            if (source.privacy >= SourcePrivacyLevel.Hidden && !request.PrivilegedAccess)
            {
                return ReliabilityFailure(InformationSourceResultCode.PrivateSourceBlocked, $"Source '{request.SourceInstanceId}' is hidden from this evaluator.", request);
            }

            SourceChainSnapshot chain = TraceSourceChain(request.SourceInstanceId, request.PrivilegedAccess);
            InformationSourceDefinition definition = ResolveDefinition(source.sourceDefinitionId);
            ReliabilityProfileData dimensions = definition?.DefaultReliability ?? ReliabilityProfileData.Default();
            List<string> diagnostics = new List<string>();

            dimensions.Set(ReliabilityDimension.ErrorRisk, source.errorRisk);
            dimensions.Set(ReliabilityDimension.DeceptionRisk, source.deceptionRisk);
            dimensions.Set(ReliabilityDimension.BiasRisk, source.biasRisk);
            dimensions.Set(ReliabilityDimension.Authenticity, VerificationScore(source.authenticityState));
            dimensions.Set(ReliabilityDimension.IdentityCertainty, VerificationScore(source.verificationState));
            dimensions.Set(ReliabilityDimension.Recency, ApplyAge(source, definition, request.WorldTimeSeconds));
            dimensions.Set(ReliabilityDimension.TransmissionIntegrity, ApplyTransmissionIntegrity(chain, definition));

            PersonSourceAssessmentData assessment = FindBestAssessment(request);
            if (assessment != null)
            {
                dimensions = dimensions.Overlay(assessment.reliability);
                dimensions.Set(ReliabilityDimension.DomainExpertise, Blend(dimensions.Get(ReliabilityDimension.DomainExpertise), assessment.authority));
                dimensions.Set(ReliabilityDimension.ErrorRisk, Blend(dimensions.Get(ReliabilityDimension.ErrorRisk), assessment.errorRisk));
                dimensions.Set(ReliabilityDimension.DeceptionRisk, Blend(dimensions.Get(ReliabilityDimension.DeceptionRisk), assessment.deceptionRisk));
                dimensions.Set(ReliabilityDimension.BiasRisk, Blend(dimensions.Get(ReliabilityDimension.BiasRisk), assessment.biasRisk));
                diagnostics.Add($"Person assessment '{assessment.assessmentId}' applied.");
            }
            else
            {
                diagnostics.Add("No person-relative assessment matched; baseline source reliability used.");
            }

            int overall = dimensions.DeriveOverall();
            int confidence = assessment == null ? 400 : assessment.confidenceInAssessment;
            diagnostics.Add($"TransmissionDepth={chain.TransmissionDepth} Original={chain.OriginalSourceId} Immediate={chain.ImmediateSourceId}.");
            return new SourceReliabilityResult(true, InformationSourceResultCode.Success, "Reliability evaluated.", request, dimensions, overall, confidence, chain, assessment == null ? null : new PersonSourceAssessmentRecord(assessment), diagnostics);
        }

        public int CalculateEffectiveEvidenceStrength(int rawStrength, SourceReliabilityResult reliability)
        {
            int raw = KnowledgeConfidence.Clamp(rawStrength);
            if (reliability == null || !reliability.Succeeded)
            {
                return raw;
            }

            return KnowledgeConfidence.Clamp((int)Math.Round(raw * (reliability.DerivedOverall / 1000d)));
        }

        public SourceIndependenceState CompareIndependence(string firstSourceId, string secondSourceId)
        {
            if (string.IsNullOrWhiteSpace(firstSourceId) || string.IsNullOrWhiteSpace(secondSourceId)
                || !sourcesById.TryGetValue(firstSourceId, out InformationSourceInstanceData first)
                || !sourcesById.TryGetValue(secondSourceId, out InformationSourceInstanceData second))
            {
                return SourceIndependenceState.Unknown;
            }

            if (string.Equals(first.sourceInstanceId, second.sourceInstanceId, StringComparison.Ordinal))
            {
                return SourceIndependenceState.SameSource;
            }

            string firstOriginal = string.IsNullOrWhiteSpace(first.originalSourceId) ? first.sourceInstanceId : first.originalSourceId;
            string secondOriginal = string.IsNullOrWhiteSpace(second.originalSourceId) ? second.sourceInstanceId : second.originalSourceId;
            if (string.Equals(firstOriginal, secondOriginal, StringComparison.Ordinal))
            {
                return SourceIndependenceState.Dependent;
            }

            if (!string.IsNullOrWhiteSpace(first.originalCreatorPersonId)
                && string.Equals(first.originalCreatorPersonId, second.originalCreatorPersonId, StringComparison.Ordinal))
            {
                return SourceIndependenceState.PartiallyIndependent;
            }

            return SourceIndependenceState.Independent;
        }

        public bool TryGetSource(string sourceInstanceId, out InformationSourceRecord record)
        {
            record = null;
            if (!sourcesById.TryGetValue(sourceInstanceId ?? string.Empty, out InformationSourceInstanceData data))
            {
                return false;
            }

            record = new InformationSourceRecord(data);
            return true;
        }

        public InformationAccessProjection<InformationSourceRecord> GetSourceProjection(string sourceInstanceId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (!TryGetSource(sourceInstanceId, out InformationSourceRecord source))
            {
                return new InformationAccessProjection<InformationSourceRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, $"Source '{sourceInstanceId}' was not found.");
            }

            string[] detailIds = SourceDetailIds(source);
            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(
                accessContext,
                BuildSourceSubject(source, InformationSubjectType.Source),
                InformationAccessMode.RevealProvenance,
                InformationAccessPurpose.Codex,
                detailIds,
                policyId);
            RedactedInformationProjection projection = accessRuntime?.Project(context, detailIds);
            if (projection == null)
            {
                return new InformationAccessProjection<InformationSourceRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            if (recordAudit)
            {
                accessRuntime.RecordAudit(projection.Decision, context, gameplayAudit: false);
            }

            InformationSourceRecord projected = projection.Decision.Denied ? null : MaterializeSource(source, projection.Decision, projection.Details);
            string visibleSubjectId = projected == null || !InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.source") ? string.Empty : projected.SourceInstanceId;
            return new InformationAccessProjection<InformationSourceRecord>(projected, projection.Decision, projection.Details, visibleSubjectId, projection.Decision.VisibleReason);
        }

        public InformationAccessProjection<SourceChainSnapshot> GetSourceChainProjection(string sourceInstanceId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (!TryGetSource(sourceInstanceId, out InformationSourceRecord source))
            {
                return new InformationAccessProjection<SourceChainSnapshot>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, $"Source '{sourceInstanceId}' was not found.");
            }

            string[] detailIds = SourceChainDetailIds(source);
            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(
                accessContext,
                BuildSourceSubject(source, InformationSubjectType.SourceChain),
                InformationAccessMode.RevealProvenance,
                InformationAccessPurpose.Codex,
                detailIds,
                policyId);
            RedactedInformationProjection projection = accessRuntime?.Project(context, detailIds);
            if (projection == null)
            {
                return new InformationAccessProjection<SourceChainSnapshot>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            if (recordAudit)
            {
                accessRuntime.RecordAudit(projection.Decision, context, gameplayAudit: false);
            }

            SourceChainSnapshot chain = null;
            if (!projection.Decision.Denied)
            {
                SourceChainSnapshot raw = TraceSourceChain(sourceInstanceId, privilegedAccess: projection.Decision.FullAccess || projection.Decision.SourceVisible);
                chain = new SourceChainSnapshot(
                    raw.Chain.Select(item => MaterializeSource(item, projection.Decision, projection.Details)).Where(item => item != null).ToArray(),
                    InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.transformations") ? raw.Transformations : Array.Empty<SourceTransformationData>(),
                    raw.OriginalHidden || !projection.Decision.SourceVisible);
            }

            string visibleSubjectId = chain == null || chain.Chain.Count == 0 || !InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.source") ? string.Empty : chain.ImmediateSourceId;
            return new InformationAccessProjection<SourceChainSnapshot>(chain, projection.Decision, projection.Details, visibleSubjectId, projection.Decision.VisibleReason);
        }

        public InformationSourceSnapshot CreateSnapshot()
        {
            return new InformationSourceSnapshot(
                OwnerId,
                SourceRevision,
                sourcesById.Values.Select(data => new InformationSourceRecord(data)).ToArray(),
                assessmentsById.Values.Select(data => new PersonSourceAssessmentRecord(data)).ToArray(),
                transformations);
        }

        public SourceChainSnapshot TraceSourceChain(string sourceInstanceId, bool privilegedAccess = false)
        {
            List<InformationSourceRecord> chain = new List<InformationSourceRecord>();
            List<SourceTransformationData> chainTransformations = new List<SourceTransformationData>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            string current = sourceInstanceId;
            bool hidden = false;

            while (!string.IsNullOrWhiteSpace(current) && sourcesById.TryGetValue(current, out InformationSourceInstanceData source) && visited.Add(current))
            {
                if (source.privacy >= SourcePrivacyLevel.Hidden && !privilegedAccess)
                {
                    hidden = true;
                    break;
                }

                chain.Add(new InformationSourceRecord(source));
                SourceTransformationData transformation = transformations.LastOrDefault(item => string.Equals(item.toSourceId, current, StringComparison.Ordinal));
                if (transformation != null)
                {
                    chainTransformations.Add(transformation.Clone());
                }

                if (source.hidesOriginal && !privilegedAccess)
                {
                    hidden = true;
                    break;
                }

                current = source.parentSourceId;
            }

            return new SourceChainSnapshot(chain, chainTransformations, hidden);
        }

        public InformationSourceSaveData CreateSaveData()
        {
            return new InformationSourceSaveData
            {
                schemaVersion = InformationSourceSaveData.CurrentSchemaVersion,
                ownerId = OwnerId,
                sourceRevision = SourceRevision,
                sources = sourcesById.Values.OrderBy(data => data.sourceInstanceId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                assessments = assessmentsById.Values.OrderBy(data => data.assessmentId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                transformations = transformations.OrderBy(data => data.transformationId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                processedTransactions = processedTransactions.Values.OrderBy(data => data.transactionId, StringComparer.Ordinal).ToArray()
            };
        }

        public InformationSourceOperationResult RestoreFromSaveData(InformationSourceSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, expectedOwnerId, out string failure))
            {
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.RestoreFailed, failure, revision: SourceRevision);
            }

            InformationSourceSaveData rollback = CreateSaveData();
            try
            {
                suppressEvents = restoring;
                registry = definitionRegistry ?? registry;
                ownerId = saveData.ownerId ?? string.Empty;
                sourcesById.Clear();
                assessmentsById.Clear();
                transformations.Clear();
                processedTransactions.Clear();

                foreach (InformationSourceInstanceData source in saveData.sources ?? Array.Empty<InformationSourceInstanceData>())
                {
                    sourcesById[source.sourceInstanceId] = source.Clone();
                }

                foreach (PersonSourceAssessmentData assessment in saveData.assessments ?? Array.Empty<PersonSourceAssessmentData>())
                {
                    assessmentsById[assessment.assessmentId] = assessment.Clone();
                }

                foreach (SourceTransformationData transformation in saveData.transformations ?? Array.Empty<SourceTransformationData>())
                {
                    transformations.Add(transformation.Clone());
                }

                foreach (InformationSourceProcessedTransactionData transaction in saveData.processedTransactions ?? Array.Empty<InformationSourceProcessedTransactionData>())
                {
                    if (!string.IsNullOrWhiteSpace(transaction.transactionId))
                    {
                        processedTransactions[TransactionKey(transaction.transactionId)] = transaction;
                    }
                }

                SourceRevision = Math.Max(0L, saveData.sourceRevision);
                return InformationSourceOperationResult.Success("Information sources restored.", string.Empty, null, null, SourceRevision, SourceRevision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, rollback.ownerId, restoring: true);
                return InformationSourceOperationResult.Failure(InformationSourceResultCode.RestoreFailed, exception.Message, revision: SourceRevision);
            }
            finally
            {
                suppressEvents = false;
            }
        }

        public static bool ValidateSaveData(InformationSourceSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Information Source save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != InformationSourceSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Information Source schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedOwnerId) && !string.Equals(saveData.ownerId, expectedOwnerId, StringComparison.Ordinal))
            {
                failureReason = $"Information Source save owner '{saveData.ownerId}' does not match expected owner '{expectedOwnerId}'.";
                return false;
            }

            HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InformationSourceInstanceData source in saveData.sources ?? Array.Empty<InformationSourceInstanceData>())
            {
                if (!ValidateSourceData(source, definitionRegistry, out failureReason) || !sourceIds.Add(source.sourceInstanceId))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Information Source save has duplicate source ID '{source?.sourceInstanceId}'." : failureReason;
                    return false;
                }
            }

            HashSet<string> assessmentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PersonSourceAssessmentData assessment in saveData.assessments ?? Array.Empty<PersonSourceAssessmentData>())
            {
                if (assessment == null || string.IsNullOrWhiteSpace(assessment.assessmentId) || !assessmentIds.Add(assessment.assessmentId))
                {
                    failureReason = $"Information Source save has missing or duplicate assessment ID '{assessment?.assessmentId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(assessment.assessingPersonId) || string.IsNullOrWhiteSpace(assessment.sourceInstanceId) || !sourceIds.Contains(assessment.sourceInstanceId))
                {
                    failureReason = $"Information Source assessment '{assessment.assessmentId}' references a missing source or Person.";
                    return false;
                }
            }

            foreach (SourceTransformationData transformation in saveData.transformations ?? Array.Empty<SourceTransformationData>())
            {
                if (transformation == null || string.IsNullOrWhiteSpace(transformation.transformationId) || !sourceIds.Contains(transformation.fromSourceId) || !sourceIds.Contains(transformation.toSourceId))
                {
                    failureReason = $"Information Source transformation '{transformation?.transformationId}' references a missing source.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateSourceData(InformationSourceInstanceData source, DefinitionRegistry definitionRegistry, out string failureReason)
        {
            failureReason = string.Empty;
            if (source == null || string.IsNullOrWhiteSpace(source.sourceInstanceId))
            {
                failureReason = "Information Source save has a missing source ID.";
                return false;
            }

            if (source.category == InformationSourceCategory.Unknown)
            {
                failureReason = $"Information Source '{source.sourceInstanceId}' has no concrete category.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(source.sourceDefinitionId)
                && (definitionRegistry == null || !definitionRegistry.TryGet(source.sourceDefinitionId, out InformationSourceDefinition _)))
            {
                failureReason = $"Information Source '{source.sourceInstanceId}' references missing definition '{source.sourceDefinitionId}'.";
                return false;
            }

            return true;
        }

        private static bool ValidateRegistrationRequest(InformationSourceRegistrationRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                failureReason = "Source registration requires a transaction ID.";
                return false;
            }

            if (request.Category == InformationSourceCategory.Unknown)
            {
                failureReason = "Source registration requires a concrete category.";
                return false;
            }

            if (request.ReferenceType != InformationSourceReferenceType.None && string.IsNullOrWhiteSpace(request.ReferencedId))
            {
                failureReason = "Typed source references require a referenced stable ID.";
                return false;
            }

            return true;
        }

        private static bool ValidateAssessmentRequest(SourceAssessmentRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.AssessingPersonId) || string.IsNullOrWhiteSpace(request.SourceInstanceId))
            {
                failureReason = "Source assessment requires transaction, assessing Person, and source IDs.";
                return false;
            }

            if (request.Reliability == null || !request.Reliability.IsValid(out failureReason))
            {
                return false;
            }

            return true;
        }

        private PersonSourceAssessmentData FindBestAssessment(SourceReliabilityRequest request)
        {
            return assessmentsById.Values
                .Where(assessment => string.Equals(assessment.assessingPersonId, request.EvaluatingPersonId ?? string.Empty, StringComparison.Ordinal))
                .Where(assessment => string.Equals(assessment.sourceInstanceId, request.SourceInstanceId ?? string.Empty, StringComparison.Ordinal))
                .Where(assessment => assessment.domain == KnowledgeDomain.Unknown || request.Domain == KnowledgeDomain.Unknown || assessment.domain == request.Domain)
                .Where(assessment => string.IsNullOrWhiteSpace(assessment.subjectId) || string.IsNullOrWhiteSpace(request.SubjectId) || string.Equals(assessment.subjectId, request.SubjectId, StringComparison.Ordinal))
                .OrderByDescending(assessment => SpecificityScore(assessment))
                .ThenByDescending(assessment => assessment.revision)
                .ThenBy(assessment => assessment.assessmentId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private InformationSourceDefinition ResolveDefinition(string definitionId)
        {
            return !string.IsNullOrWhiteSpace(definitionId) && registry != null && registry.TryGet(definitionId, out InformationSourceDefinition definition)
                ? definition
                : null;
        }

        private InformationSourceDefinition ResolveDefinition(InformationSourceCategory category)
        {
            if (registry == null || category == InformationSourceCategory.Unknown)
            {
                return null;
            }

            InformationSourceDefinition[] matches = registry.DefinitionsById.Values
                .OfType<InformationSourceDefinition>()
                .Where(definition => definition.Category == category)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static int SpecificityScore(PersonSourceAssessmentData assessment)
        {
            int score = 0;
            if (assessment.domain != KnowledgeDomain.Unknown) score += 10;
            if (!string.IsNullOrWhiteSpace(assessment.subjectId)) score += 5;
            if (!string.IsNullOrWhiteSpace(assessment.methodId)) score += 3;
            return score;
        }

        private static int VerificationScore(SourceVerificationState state)
        {
            return state switch
            {
                SourceVerificationState.Verified => 900,
                SourceVerificationState.PartiallyVerified => 650,
                SourceVerificationState.Claimed => 500,
                SourceVerificationState.Unverified => 350,
                SourceVerificationState.Disputed => 250,
                SourceVerificationState.Forged => 50,
                SourceVerificationState.Superseded => 300,
                _ => 400
            };
        }

        private static int ApplyAge(InformationSourceInstanceData source, InformationSourceDefinition definition, double worldTime)
        {
            if (definition == null)
            {
                return 0;
            }

            if (definition.StalenessPolicy == KnowledgeStalenessPolicy.NeverStale || definition.StalenessHalfLifeSeconds <= 0d)
            {
                return 900;
            }

            double sourceTime = Math.Max(source.creationWorldTimeSeconds, Math.Max(source.observationWorldTimeSeconds, source.transmissionWorldTimeSeconds));
            double age = Math.Max(0d, worldTime - sourceTime);
            double halfLifeSeconds = definition.StalenessHalfLifeSeconds;
            double halfLives = age / halfLifeSeconds;
            int score = 900 - (int)Math.Round(halfLives * 250d);
            return KnowledgeConfidence.Clamp(score);
        }

        private static int ApplyTransmissionIntegrity(SourceChainSnapshot chain, InformationSourceDefinition definition)
        {
            int penalty = definition == null ? KnowledgeConfidence.Maximum : definition.TransmissionPenaltyPerHop;
            int score = 900 - Math.Max(0, chain.TransmissionDepth) * penalty;
            foreach (SourceTransformationData transformation in chain.Transformations)
            {
                score = Blend(score, transformation.quality);
            }

            return KnowledgeConfidence.Clamp(score);
        }

        private static int Blend(int first, int second)
        {
            return KnowledgeConfidence.Clamp((int)Math.Round((KnowledgeConfidence.Clamp(first) + KnowledgeConfidence.Clamp(second)) / 2d));
        }

        private SourceReliabilityResult ReliabilityFailure(InformationSourceResultCode code, string message, SourceReliabilityRequest request)
        {
            return new SourceReliabilityResult(false, code, message, request, ReliabilityProfileData.Default(), 0, 0, null, null, Array.Empty<string>());
        }

        private static InformationSubjectReferenceData BuildSourceSubject(InformationSourceRecord source, InformationSubjectType subjectType)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = subjectType,
                subjectId = source?.SourceInstanceId ?? string.Empty,
                parentSubjectId = source?.OriginalSourceId ?? string.Empty,
                ownerPersonId = source?.Data.holderPersonId ?? string.Empty,
                controllingEntityId = source?.Data.authorityClassification ?? string.Empty,
                tags = source?.Data.tags == null ? Array.Empty<string>() : source.Data.tags.ToArray()
            };
        }

        private static string[] SourceDetailIds(InformationSourceRecord source)
        {
            return new[]
            {
                "detail.summary",
                "detail.source",
                "detail.source-identity",
                "detail.original-source",
                "detail.creator",
                "detail.observer",
                "detail.holder",
                "detail.transmitter",
                "detail.subject",
                "detail.method",
                "detail.privacy",
                "detail.reliability",
                "detail.provenance"
            };
        }

        private static string[] SourceChainDetailIds(InformationSourceRecord source)
        {
            return SourceDetailIds(source)
                .Concat(new[] { "detail.chain", "detail.transformations", "detail.chain-depth" })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static InformationSourceRecord MaterializeSource(InformationSourceRecord source, InformationAccessDecision decision, IReadOnlyDictionary<string, InformationRedactionState> details)
        {
            if (source == null)
            {
                return null;
            }

            if (details.Values.All(state => state == InformationRedactionState.Visible) && decision.SourceVisible)
            {
                return source;
            }

            InformationSourceInstanceData data = source.Data.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.source")) data.sourceInstanceId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.source-identity") || !decision.SourceVisible)
            {
                data.referencedId = string.Empty;
                data.sourceDefinitionId = string.Empty;
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.original-source") || !decision.SourceVisible)
            {
                data.originalSourceId = string.Empty;
                data.parentSourceId = string.Empty;
                data.hidesOriginal = true;
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.creator") || !decision.SourceVisible) data.originalCreatorPersonId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.observer") || !decision.SourceVisible) data.observerPersonId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.holder")) data.holderPersonId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.transmitter") || !decision.SourceVisible) data.transmitterPersonId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.subject")) { data.domain = KnowledgeDomain.Unknown; data.subjectId = string.Empty; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.method")) data.methodId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.privacy")) data.authorityClassification = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.reliability")) { data.errorRisk = 0; data.deceptionRisk = 0; data.biasRisk = 0; data.biasProfileId = string.Empty; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.provenance")) { data.supersedesSourceId = string.Empty; data.correctedBySourceId = string.Empty; }
            return new InformationSourceRecord(data);
        }

        private void RememberTransaction(string transactionId, InformationSourceResultCode code, string sourceId, string assessmentId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedTransactions[TransactionKey(transactionId)] = new InformationSourceProcessedTransactionData
            {
                transactionId = transactionId,
                code = code,
                sourceInstanceId = sourceId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                revision = SourceRevision
            };
        }

        private InformationSourceOperationResult DuplicateResult(InformationSourceProcessedTransactionData processed, string transactionId)
        {
            InformationSourceRecord source = !string.IsNullOrWhiteSpace(processed.sourceInstanceId) && sourcesById.TryGetValue(processed.sourceInstanceId, out InformationSourceInstanceData sourceData)
                ? new InformationSourceRecord(sourceData)
                : null;
            PersonSourceAssessmentRecord assessment = !string.IsNullOrWhiteSpace(processed.assessmentId) && assessmentsById.TryGetValue(processed.assessmentId, out PersonSourceAssessmentData assessmentData)
                ? new PersonSourceAssessmentRecord(assessmentData)
                : null;
            return InformationSourceOperationResult.Success("Duplicate Information Source transaction ignored.", transactionId, source, assessment, SourceRevision, SourceRevision, duplicate: true);
        }

        private static string TransactionKey(string transactionId)
        {
            return transactionId ?? string.Empty;
        }

        private static string StableSourceId(InformationSourceCategory category, string referencedId, string transactionId)
        {
            string seed = string.IsNullOrWhiteSpace(referencedId) ? transactionId : referencedId;
            return $"information-source.runtime.{category.ToString().ToLowerInvariant()}.{Sanitize(seed)}";
        }

        private static string StableAssessmentId(string personId, string sourceId, KnowledgeDomain domain, string subjectId)
        {
            return $"source-assessment.{Sanitize(personId)}.{Sanitize(sourceId)}.{domain.ToString().ToLowerInvariant()}.{Sanitize(subjectId)}";
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "none";
            }

            char[] chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            return new string(chars).Trim('-');
        }

        private void RaiseChanged(InformationSourceOperationResult result, bool restoring)
        {
            if (!restoring && !suppressEvents)
            {
                SourcesChanged?.Invoke(this, result);
            }
        }
    }
}
