using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Quests
{
    public sealed class QuestSourceRuntime : IDisposable
    {
        private readonly Dictionary<string, QuestSourceRecordData> sourcesById = new Dictionary<string, QuestSourceRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestListingRecordData> listingsById = new Dictionary<string, QuestListingRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestSourceDiscoveryRecordData> discoveriesById = new Dictionary<string, QuestSourceDiscoveryRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestSourceAssociationRecordData> associationsById = new Dictionary<string, QuestSourceAssociationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestSourceTransactionData> transactionsById = new Dictionary<string, QuestSourceTransactionData>(StringComparer.Ordinal);
        private readonly List<QuestSourceEventData> events = new List<QuestSourceEventData>();
        private readonly Dictionary<string, List<string>> listingsBySource = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> listingsByQuest = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> sourcesByInteractionPoint = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> sourcesByLocation = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private QuestRuntime questRuntime;
        private QuestParticipationRuntime participationRuntime;
        private DefinitionRegistry registry;
        private string worldId;
        private bool disposed;
        private long revision;

        public QuestSourceRuntime(QuestRuntime quests = null, QuestParticipationRuntime participation = null, DefinitionRegistry definitionRegistry = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(quests, participation, definitionRegistry, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int SourceCount => sourcesById.Count;
        public int ListingCount => listingsById.Count;
        public int DiscoveryCount => discoveriesById.Count;
        public int AssociationCount => associationsById.Count;
        public IReadOnlyList<QuestSourceEventData> Events => events.Select(value => value.Clone()).ToArray();

        public void Configure(QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry definitionRegistry, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            questRuntime = quests;
            participationRuntime = participation;
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId;
        }

        public QuestSourceOperationResult CreateSource(QuestSourceCreateRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestSourceCreateRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestSourceOperationResult revisionFailure)) return revisionFailure;
            if (registry == null) return Fail(QuestSourceOperationStatus.MissingDefinitionRegistry, "Quest source runtime has no definition registry.");
            if (string.IsNullOrWhiteSpace(request.questSourceDefinitionId)) return Fail(QuestSourceOperationStatus.InvalidRequest, "Quest source creation requires a definition ID.");
            if (!registry.TryGet(N(request.questSourceDefinitionId), out QuestSourceDefinition definition)) return Fail(QuestSourceOperationStatus.MissingDefinition, $"Quest Source definition '{N(request.questSourceDefinitionId)}' is missing.");

            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestSourceOperationResult duplicate)) return duplicate;

            string sourceId = string.IsNullOrWhiteSpace(request.questSourceId) ? BuildSourceId(definition.Id, request.hostLocationId, request.interactionPointId) : N(request.questSourceId);
            if (sourcesById.ContainsKey(sourceId)) return Fail(QuestSourceOperationStatus.InvalidRequest, $"Quest Source '{sourceId}' already exists.");

            QuestSourceRecordData record = new QuestSourceRecordData
            {
                questSourceId = sourceId,
                questSourceDefinitionId = definition.Id,
                worldId = worldId,
                lifecycleState = request.initialLifecycleState == QuestSourceLifecycleState.Unknown ? QuestSourceLifecycleState.Active : request.initialLifecycleState,
                hostLocationId = N(request.hostLocationId),
                interactionPointId = N(request.interactionPointId),
                operatingOrganizationId = N(request.operatingOrganizationId),
                operatingGovernmentId = N(request.operatingGovernmentId),
                operatingFactionId = N(request.operatingFactionId),
                operatingBusinessId = N(request.operatingBusinessId),
                operatingOfficeId = N(request.operatingOfficeId),
                visibility = request.visibility ?? definition.DefaultVisibility,
                createdWorldTime = request.worldTime,
                sceneBindingKey = N(request.sceneBindingKey),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return QuestSourceOperationResult.Success("Quest source previewed.", revision, revision, source: record, preview: true);

            long before = revision;
            sourcesById[sourceId] = record.Clone();
            revision++;
            RecordTransaction(transactionId, "CreateSource", sourceId, string.Empty, string.Empty);
            RecordEvent(transactionId, QuestSourceEventKind.SourceCreated, sourceId, string.Empty, string.Empty, QuestSourceLifecycleState.Unknown, record.lifecycleState, QuestListingLifecycleState.Unknown, QuestListingLifecycleState.Unknown, request.worldTime, request.provenanceId);
            RebuildIndexes();
            return QuestSourceOperationResult.Success("Quest source created.", before, revision, source: record);
        }

        public QuestSourceOperationResult TransitionSource(QuestSourceLifecycleRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestSourceLifecycleRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestSourceOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestSourceOperationResult duplicate)) return duplicate;
            if (!sourcesById.TryGetValue(N(request.questSourceId), out QuestSourceRecordData source)) return Fail(QuestSourceOperationStatus.MissingSource, $"Quest Source '{N(request.questSourceId)}' is missing.");
            if (request.targetState == QuestSourceLifecycleState.Unknown) return Fail(QuestSourceOperationStatus.InvalidRequest, "Quest source lifecycle transition requires a concrete state.");

            QuestSourceRecordData changed = source.Clone();
            QuestSourceLifecycleState beforeState = changed.lifecycleState;
            changed.lifecycleState = request.targetState;
            if (request.targetState == QuestSourceLifecycleState.Retired || request.targetState == QuestSourceLifecycleState.Historical || request.targetState == QuestSourceLifecycleState.Invalid)
            {
                changed.retiredWorldTime = request.worldTime;
            }

            changed.revision++;
            if (request.preview) return QuestSourceOperationResult.Success("Quest source lifecycle previewed.", revision, revision, source: changed, preview: true);

            long before = revision;
            sourcesById[changed.questSourceId] = changed;
            revision++;
            RecordTransaction(transactionId, "TransitionSource", changed.questSourceId, string.Empty, string.Empty);
            RecordEvent(transactionId, QuestSourceEventKind.SourceLifecycleChanged, changed.questSourceId, string.Empty, string.Empty, beforeState, changed.lifecycleState, QuestListingLifecycleState.Unknown, QuestListingLifecycleState.Unknown, request.worldTime, request.provenanceId);
            RebuildIndexes();
            return QuestSourceOperationResult.Success("Quest source lifecycle changed.", before, revision, source: changed);
        }

        public QuestSourceOperationResult PublishListing(QuestListingPublishRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestListingPublishRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestSourceOperationResult revisionFailure)) return revisionFailure;
            if (questRuntime == null) return Fail(QuestSourceOperationStatus.MissingQuestRuntime, "Quest runtime is required to publish a listing.");
            if (registry == null) return Fail(QuestSourceOperationStatus.MissingDefinitionRegistry, "Quest source runtime has no definition registry.");
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestSourceOperationResult duplicate)) return duplicate;
            if (!sourcesById.TryGetValue(N(request.questSourceId), out QuestSourceRecordData source)) return Fail(QuestSourceOperationStatus.MissingSource, $"Quest Source '{N(request.questSourceId)}' is missing.");
            if (!IsActive(source.lifecycleState)) return Fail(QuestSourceOperationStatus.SourceInactive, $"Quest Source '{source.questSourceId}' is not active.");
            if (!registry.TryGet(source.questSourceDefinitionId, out QuestSourceDefinition sourceDefinition)) return Fail(QuestSourceOperationStatus.MissingDefinition, $"Quest Source definition '{source.questSourceDefinitionId}' is missing.");
            if (!questRuntime.TryGetSnapshot(N(request.questId), out QuestSnapshot quest)) return Fail(QuestSourceOperationStatus.MissingQuest, $"Quest '{N(request.questId)}' is missing.");
            if (!registry.TryGet(quest.QuestDefinitionId, out QuestDefinition questDefinition)) return Fail(QuestSourceOperationStatus.MissingDefinition, $"Quest definition '{quest.QuestDefinitionId}' is missing.");

            if (!CanPublish(sourceDefinition, quest, questDefinition, request, out QuestSourceOperationStatus publishStatus, out string publishFailure))
            {
                return Fail(publishStatus, publishFailure);
            }

            string listingId = string.IsNullOrWhiteSpace(request.questListingId) ? BuildListingId(source.questSourceId, quest.QuestId) : N(request.questListingId);
            bool duplicateActive = ActiveListingsForSource(source.questSourceId).Any(value => string.Equals(value.questId, quest.QuestId, StringComparison.Ordinal));
            if (duplicateActive && sourceDefinition.PublicationPolicy.duplicatePolicy == QuestListingDuplicatePolicy.RejectActiveDuplicate)
            {
                return Fail(QuestSourceOperationStatus.Duplicate, "An active listing for this Quest already exists on this source.");
            }

            if (listingsById.ContainsKey(listingId)) return Fail(QuestSourceOperationStatus.InvalidRequest, $"Quest Listing '{listingId}' already exists.");

            int capacity = sourceDefinition.PublicationPolicy.maxActiveListings;
            if (capacity >= 0 && ActiveListingsForSource(source.questSourceId).Count() >= capacity)
            {
                return Fail(QuestSourceOperationStatus.SourceCapacityExceeded, $"Quest Source '{source.questSourceId}' has reached listing capacity {capacity}.");
            }

            double expiration = request.expirationWorldTime;
            QuestListingExpirationPolicy expirationPolicy = sourceDefinition.PublicationPolicy.expirationPolicy;
            if (expiration < 0d && expirationPolicy == QuestListingExpirationPolicy.SourceDefaultDuration && sourceDefinition.PublicationPolicy.defaultListingDuration >= 0d)
            {
                expiration = request.worldTime + sourceDefinition.PublicationPolicy.defaultListingDuration;
            }

            QuestListingRecordData listing = new QuestListingRecordData
            {
                questListingId = listingId,
                questId = quest.QuestId,
                questSourceId = source.questSourceId,
                worldId = worldId,
                lifecycleState = QuestListingLifecycleState.Published,
                publishedWorldTime = request.worldTime,
                expirationWorldTime = expiration,
                priority = request.priority,
                visibility = request.visibility ?? source.visibility,
                eligibilityDisplayPolicy = sourceDefinition.EligibilityDisplayPolicy,
                acceptedDisplayPolicy = sourceDefinition.PublicationPolicy.acceptedListingPolicy,
                repeatableDisplayPolicy = sourceDefinition.PublicationPolicy.repeatableListingPolicy,
                intendedAudience = request.intendedAudience?.Clone() ?? new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                publisherPersonId = N(request.publisherPersonId),
                publisherAuthorityId = N(request.publisherAuthorityId),
                sourceEventId = N(request.sourceEventId),
                provenanceId = N(request.provenanceId),
                sourceRevisionAtPublication = source.revision,
                revision = 1L
            };

            if (request.preview) return QuestSourceOperationResult.Success("Quest listing previewed.", revision, revision, source: source, listing: listing, preview: true);

            long before = revision;
            listingsById[listingId] = listing.Clone();
            revision++;
            RecordTransaction(transactionId, "PublishListing", source.questSourceId, listingId, quest.QuestId);
            RecordEvent(transactionId, QuestSourceEventKind.ListingPublished, source.questSourceId, listingId, quest.QuestId, source.lifecycleState, source.lifecycleState, QuestListingLifecycleState.Unknown, listing.lifecycleState, request.worldTime, request.provenanceId);
            RebuildIndexes();
            return QuestSourceOperationResult.Success("Quest listing published.", before, revision, source: source, listing: listing);
        }

        public QuestSourceOperationResult TransitionListing(QuestListingLifecycleRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestListingLifecycleRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestSourceOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestSourceOperationResult duplicate)) return duplicate;
            if (!listingsById.TryGetValue(N(request.questListingId), out QuestListingRecordData listing)) return Fail(QuestSourceOperationStatus.MissingListing, $"Quest Listing '{N(request.questListingId)}' is missing.");
            if (!sourcesById.TryGetValue(listing.questSourceId, out QuestSourceRecordData source)) return Fail(QuestSourceOperationStatus.MissingSource, $"Quest Source '{listing.questSourceId}' is missing.");
            if (request.targetState == QuestListingLifecycleState.Unknown || request.targetState == QuestListingLifecycleState.Draft) return Fail(QuestSourceOperationStatus.InvalidRequest, "Listing lifecycle transition requires a terminal or active concrete state.");

            QuestListingRecordData changed = listing.Clone();
            QuestListingLifecycleState beforeState = changed.lifecycleState;
            changed.lifecycleState = request.targetState;
            if (!IsActive(changed.lifecycleState))
            {
                changed.endedWorldTime = request.worldTime;
            }

            changed.revision++;
            if (request.preview) return QuestSourceOperationResult.Success("Quest listing lifecycle previewed.", revision, revision, source: source, listing: changed, preview: true);

            long before = revision;
            listingsById[changed.questListingId] = changed;
            revision++;
            RecordTransaction(transactionId, "TransitionListing", source.questSourceId, changed.questListingId, changed.questId);
            RecordEvent(transactionId, EventFromListingState(changed.lifecycleState), source.questSourceId, changed.questListingId, changed.questId, source.lifecycleState, source.lifecycleState, beforeState, changed.lifecycleState, request.worldTime, request.provenanceId);
            RebuildIndexes();
            return QuestSourceOperationResult.Success("Quest listing lifecycle changed.", before, revision, source: source, listing: changed);
        }

        public IReadOnlyList<QuestSourceOperationResult> EvaluateExpirations(double worldTime, string transactionPrefix = "tx.quest-source.expire")
        {
            return listingsById.Values
                .Where(value => value.lifecycleState == QuestListingLifecycleState.Published && value.expirationWorldTime >= 0d && worldTime >= value.expirationWorldTime)
                .OrderBy(value => value.expirationWorldTime)
                .ThenBy(value => value.questListingId, StringComparer.Ordinal)
                .Select(value => TransitionListing(new QuestListingLifecycleRequest
                {
                    transactionId = $"{transactionPrefix}.{value.questListingId}",
                    questListingId = value.questListingId,
                    targetState = QuestListingLifecycleState.Expired,
                    worldTime = worldTime
                }))
                .ToArray();
        }

        public QuestSourceBrowseResult BrowseSource(QuestSourceBrowseRequest request)
        {
            if (disposed) return new QuestSourceBrowseResult(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.", null, Array.Empty<QuestVisibleListingSnapshot>(), 0, revision);
            request ??= new QuestSourceBrowseRequest();
            if (!sourcesById.TryGetValue(N(request.questSourceId), out QuestSourceRecordData source)) return new QuestSourceBrowseResult(QuestSourceOperationStatus.MissingSource, $"Quest Source '{N(request.questSourceId)}' is missing.", null, Array.Empty<QuestVisibleListingSnapshot>(), 0, revision);
            if (request.expectedSourceRevision >= 0L && request.expectedSourceRevision != source.revision) return new QuestSourceBrowseResult(QuestSourceOperationStatus.RevisionConflict, $"Expected source revision {request.expectedSourceRevision}, actual {source.revision}.", null, Array.Empty<QuestVisibleListingSnapshot>(), 0, revision);
            if (!CanSee(source.visibility, request.access, request.requesterPersonId, source)) return new QuestSourceBrowseResult(QuestSourceOperationStatus.VisibilityDenied, "Quest Source is not visible to this requester.", null, Array.Empty<QuestVisibleListingSnapshot>(), 0, revision);

            IReadOnlyList<QuestVisibleListingSnapshot> visible = BuildVisibleListings(source, request)
                .OrderBy(value => value.Listing.Priority)
                .ThenBy(value => value.Listing.PublishedWorldTime)
                .ThenBy(value => value.Quest?.QuestDefinitionId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.Quest?.QuestId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.Listing.QuestListingId, StringComparer.Ordinal)
                .ToArray();

            int offset = Math.Max(0, request.offset);
            int limit = request.limit <= 0 ? visible.Count : Math.Max(1, request.limit);
            QuestVisibleListingSnapshot[] page = visible.Skip(offset).Take(limit).ToArray();
            int next = offset + page.Length < visible.Count ? offset + page.Length : -1;
            if (request.recordDiscovery && !string.IsNullOrWhiteSpace(request.requesterPersonId))
            {
                RecordDiscovery(new QuestSourceDiscoveryRequest
                {
                    transactionId = string.IsNullOrWhiteSpace(request.transactionId) ? $"tx.quest-source.browse.{source.questSourceId}.{request.requesterPersonId}" : request.transactionId,
                    personId = request.requesterPersonId,
                    questSourceId = source.questSourceId,
                    discoveryKind = QuestSourceDiscoveryKind.SourceKnown,
                    subject = QuestInformationSubject.Source(source.questSourceId, source.questSourceDefinitionId, SourceControllingEntity(source)),
                    worldTime = request.worldTime,
                    provenanceId = "quest-source.browse"
                });
            }

            return new QuestSourceBrowseResult(QuestSourceOperationStatus.Succeeded, "Quest Source browsed.", new QuestSourceSnapshot(source), page, next, revision);
        }

        public QuestListingInspectionResult InspectListing(QuestListingInspectRequest request)
        {
            if (disposed) return new QuestListingInspectionResult(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.", null, null, Array.Empty<QuestRewardEntitlementSnapshot>(), Array.Empty<QuestSourceDiscoveryRecordData>(), revision);
            request ??= new QuestListingInspectRequest();
            if (!listingsById.TryGetValue(N(request.questListingId), out QuestListingRecordData listing)) return new QuestListingInspectionResult(QuestSourceOperationStatus.MissingListing, $"Quest Listing '{N(request.questListingId)}' is missing.", null, null, Array.Empty<QuestRewardEntitlementSnapshot>(), Array.Empty<QuestSourceDiscoveryRecordData>(), revision);
            if (!sourcesById.TryGetValue(listing.questSourceId, out QuestSourceRecordData source)) return new QuestListingInspectionResult(QuestSourceOperationStatus.MissingSource, $"Quest Source '{listing.questSourceId}' is missing.", null, null, Array.Empty<QuestRewardEntitlementSnapshot>(), Array.Empty<QuestSourceDiscoveryRecordData>(), revision);
            if (!string.IsNullOrWhiteSpace(request.questSourceId) && !string.Equals(request.questSourceId, source.questSourceId, StringComparison.Ordinal)) return new QuestListingInspectionResult(QuestSourceOperationStatus.InvalidRequest, "Listing does not belong to requested source.", null, null, Array.Empty<QuestRewardEntitlementSnapshot>(), Array.Empty<QuestSourceDiscoveryRecordData>(), revision);
            if (!CanSee(source.visibility, request.access, request.requesterPersonId, source) || !CanSee(listing.visibility, request.access, request.requesterPersonId, source)) return new QuestListingInspectionResult(QuestSourceOperationStatus.VisibilityDenied, "Quest listing is not visible to this requester.", null, null, Array.Empty<QuestRewardEntitlementSnapshot>(), Array.Empty<QuestSourceDiscoveryRecordData>(), revision);

            QuestSourceBrowseRequest browseRequest = new QuestSourceBrowseRequest
            {
                questSourceId = source.questSourceId,
                requesterPersonId = request.requesterPersonId,
                access = request.access,
                eligibilityContext = request.eligibilityContext,
                worldTime = request.worldTime,
                includeHistorical = true,
                includeIneligible = true,
                limit = 0
            };
            QuestVisibleListingSnapshot visible = BuildVisibleListings(source, browseRequest).FirstOrDefault(value => value.Listing.QuestListingId == listing.questListingId);
            if (visible == null) return new QuestListingInspectionResult(QuestSourceOperationStatus.VisibilityDenied, "Quest listing is concealed from this requester.", new QuestSourceSnapshot(source), null, Array.Empty<QuestRewardEntitlementSnapshot>(), Array.Empty<QuestSourceDiscoveryRecordData>(), revision);

            List<QuestSourceDiscoveryRecordData> discoveries = new List<QuestSourceDiscoveryRecordData>();
            if (request.recordDiscovery && !string.IsNullOrWhiteSpace(request.requesterPersonId))
            {
                QuestSourceOperationResult listingDiscovery = RecordDiscovery(new QuestSourceDiscoveryRequest
                {
                    transactionId = string.IsNullOrWhiteSpace(request.transactionId) ? $"tx.quest-source.inspect.{listing.questListingId}.{request.requesterPersonId}.listing" : $"{request.transactionId}.listing",
                    personId = request.requesterPersonId,
                    questSourceId = source.questSourceId,
                    questListingId = listing.questListingId,
                    questId = listing.questId,
                    discoveryKind = QuestSourceDiscoveryKind.ListingDetailsKnown,
                    subject = QuestInformationSubject.Quest(listing.questId, visible.Quest?.QuestDefinitionId, request.requesterPersonId, visible.Quest?.Issuer?.issuerId, visible.Quest?.TagIds),
                    worldTime = request.worldTime,
                    provenanceId = "quest-source.inspect"
                });
                if (listingDiscovery.Discovery != null)
                {
                    discoveries.Add(listingDiscovery.Discovery);
                }
            }

            return new QuestListingInspectionResult(QuestSourceOperationStatus.Succeeded, "Quest listing inspected.", new QuestSourceSnapshot(source), visible, Array.Empty<QuestRewardEntitlementSnapshot>(), discoveries, revision);
        }

        public QuestSourceOperationResult AcceptFromSource(QuestSourceAcceptRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestSourceAcceptRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestSourceOperationResult revisionFailure)) return revisionFailure;
            if (participationRuntime == null) return Fail(QuestSourceOperationStatus.MissingParticipationRuntime, "Quest participation runtime is required to accept a source listing.");
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestSourceOperationResult duplicate)) return duplicate;
            if (!listingsById.TryGetValue(N(request.questListingId), out QuestListingRecordData listing)) return Fail(QuestSourceOperationStatus.MissingListing, $"Quest Listing '{N(request.questListingId)}' is missing.");
            if (!sourcesById.TryGetValue(listing.questSourceId, out QuestSourceRecordData source)) return Fail(QuestSourceOperationStatus.MissingSource, $"Quest Source '{listing.questSourceId}' is missing.");
            if (!IsActive(source.lifecycleState)) return Fail(QuestSourceOperationStatus.SourceInactive, $"Quest Source '{source.questSourceId}' is not active.");
            if (!IsActive(listing.lifecycleState)) return Fail(QuestSourceOperationStatus.ListingInactive, $"Quest Listing '{listing.questListingId}' is not active.");

            QuestEligibilityContext context = request.eligibilityContext?.Clone() ?? new QuestEligibilityContext { personId = request.personId, privilegedDiagnostics = true, worldTime = request.worldTime };
            context.personId = N(request.personId);
            context.interactionPointId = string.IsNullOrWhiteSpace(context.interactionPointId) ? source.interactionPointId : context.interactionPointId;
            context.locationId = string.IsNullOrWhiteSpace(context.locationId) ? source.hostLocationId : context.locationId;
            QuestEligibilityResult eligibility = participationRuntime.EvaluateEligibility(listing.questId, context);
            if (!eligibility.Eligible) return Fail(QuestSourceOperationStatus.EligibilityDenied, "Requester is not eligible to accept this source listing.");

            if (request.preview) return QuestSourceOperationResult.Success("Quest source acceptance previewed.", revision, revision, source: source, listing: listing, preview: true);

            QuestIssuerReferenceData issuer = IssuerFromSource(source);
            QuestIssuerReferenceData provider = issuer.Clone();
            provider.actingPersonId = listing.publisherPersonId;

            QuestParticipationOperationResult offer = participationRuntime.CreateOffer(new QuestOfferRequest
            {
                transactionId = $"{transactionId}.offer",
                questId = listing.questId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = request.personId },
                institutionalIssuer = issuer,
                offeringProvider = provider,
                channel = OfferChannelFromSource(source),
                sourceInteractionPointId = source.interactionPointId,
                sourceLocationId = source.hostLocationId,
                authorityBasisId = request.authorityBasisId,
                eligibilityContext = context,
                worldTime = request.worldTime,
                visibility = ToQuestVisibility(listing.visibility)
            });
            if (!offer.Succeeded || offer.Offer == null) return Fail(QuestSourceOperationStatus.OfferRejected, offer.Message);

            QuestParticipationOperationResult accept = participationRuntime.AcceptOffer(new QuestAcceptOfferRequest
            {
                transactionId = $"{transactionId}.accept",
                offerId = offer.Offer.OfferId,
                personId = request.personId,
                explicitConsent = request.explicitConsent,
                consentRecordId = request.consentRecordId,
                authorityBasisId = request.authorityBasisId,
                eligibilityContext = context,
                worldTime = request.worldTime
            });
            if (!accept.Succeeded || accept.Assignment == null) return Fail(QuestSourceOperationStatus.AcceptanceRejected, accept.Message);

            QuestListingRecordData changed = listing.Clone();
            QuestListingLifecycleState beforeState = changed.lifecycleState;
            changed.claimedAssignmentId = accept.Assignment.AssignmentId;
            if (changed.acceptedDisplayPolicy == QuestAcceptedListingDisplayPolicy.HideWhenAccepted || changed.acceptedDisplayPolicy == QuestAcceptedListingDisplayPolicy.ShowAsTaken)
            {
                changed.lifecycleState = QuestListingLifecycleState.Claimed;
                changed.endedWorldTime = request.worldTime;
            }

            changed.revision++;
            long before = revision;
            listingsById[changed.questListingId] = changed;
            revision++;
            RecordTransaction(transactionId, "AcceptFromSource", source.questSourceId, changed.questListingId, changed.questId);
            RecordEvent(transactionId, QuestSourceEventKind.ListingClaimed, source.questSourceId, changed.questListingId, changed.questId, source.lifecycleState, source.lifecycleState, beforeState, changed.lifecycleState, request.worldTime, "quest-source.acceptance");
            RebuildIndexes();
            return QuestSourceOperationResult.Success("Quest listing accepted through source.", before, revision, source: source, listing: changed, assignment: accept.Assignment);
        }

        public QuestSourceOperationResult RecordDiscovery(QuestSourceDiscoveryRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestSourceDiscoveryRequest();
            if (string.IsNullOrWhiteSpace(request.personId)) return Fail(QuestSourceOperationStatus.InvalidRequest, "Quest source discovery requires a Person ID.");
            string discoveryId = BuildDiscoveryId(request.personId, request.questSourceId, request.questListingId, request.questId, request.discoveryKind);
            if (discoveriesById.TryGetValue(discoveryId, out QuestSourceDiscoveryRecordData existing)) return QuestSourceOperationResult.Success("Quest source discovery already recorded.", revision, revision, discovery: existing, duplicate: true);

            QuestSourceDiscoveryRecordData record = new QuestSourceDiscoveryRecordData
            {
                discoveryId = discoveryId,
                personId = N(request.personId),
                questSourceId = N(request.questSourceId),
                questListingId = N(request.questListingId),
                questId = N(request.questId),
                discoveryKind = request.discoveryKind == QuestSourceDiscoveryKind.Unknown ? QuestSourceDiscoveryKind.SourceKnown : request.discoveryKind,
                    subject = request.subject?.Clone() ?? SourceSubject(request.questSourceId),
                knowledgeReferenceId = N(request.knowledgeReferenceId),
                worldTime = request.worldTime,
                transactionId = N(request.transactionId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return QuestSourceOperationResult.Success("Quest source discovery previewed.", revision, revision, discovery: record, preview: true);

            long before = revision;
            discoveriesById[discoveryId] = record.Clone();
            revision++;
            RecordEvent(request.transactionId, QuestSourceEventKind.DiscoveryRecorded, record.questSourceId, record.questListingId, record.questId, QuestSourceLifecycleState.Unknown, QuestSourceLifecycleState.Unknown, QuestListingLifecycleState.Unknown, QuestListingLifecycleState.Unknown, request.worldTime, request.provenanceId);
            return QuestSourceOperationResult.Success("Quest source discovery recorded.", before, revision, discovery: record);
        }

        public QuestSourceOperationResult RecordAssociation(QuestSourceAssociationRequest request)
        {
            if (disposed) return Fail(QuestSourceOperationStatus.Disposed, "Quest source runtime is disposed.");
            request ??= new QuestSourceAssociationRequest();
            string associationId = BuildAssociationId(request.role, request.questSourceId, request.questListingId, request.questId, request.assignmentId, request.terminalOutcomeId, request.rewardEntitlementId);
            if (associationsById.TryGetValue(associationId, out QuestSourceAssociationRecordData existing)) return QuestSourceOperationResult.Success("Quest source association already recorded.", revision, revision, association: existing, duplicate: true);

            QuestSourceAssociationRecordData record = new QuestSourceAssociationRecordData
            {
                associationId = associationId,
                role = request.role == QuestSourceRole.Unknown ? QuestSourceRole.Custom : request.role,
                questSourceId = N(request.questSourceId),
                questListingId = N(request.questListingId),
                questId = N(request.questId),
                assignmentId = N(request.assignmentId),
                terminalOutcomeId = N(request.terminalOutcomeId),
                rewardEntitlementId = N(request.rewardEntitlementId),
                interactionPointId = N(request.interactionPointId),
                worldTime = request.worldTime,
                transactionId = N(request.transactionId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return QuestSourceOperationResult.Success("Quest source association previewed.", revision, revision, association: record, preview: true);

            long before = revision;
            associationsById[associationId] = record.Clone();
            revision++;
            RecordEvent(request.transactionId, QuestSourceEventKind.SourceAssociationRecorded, record.questSourceId, record.questListingId, record.questId, QuestSourceLifecycleState.Unknown, QuestSourceLifecycleState.Unknown, QuestListingLifecycleState.Unknown, QuestListingLifecycleState.Unknown, request.worldTime, request.provenanceId);
            return QuestSourceOperationResult.Success("Quest source association recorded.", before, revision, association: record);
        }

        public bool TryGetSource(string questSourceId, out QuestSourceSnapshot snapshot)
        {
            snapshot = null;
            if (!sourcesById.TryGetValue(N(questSourceId), out QuestSourceRecordData source)) return false;
            snapshot = new QuestSourceSnapshot(source);
            return true;
        }

        public bool TryGetListing(string questListingId, out QuestListingSnapshot snapshot)
        {
            snapshot = null;
            if (!listingsById.TryGetValue(N(questListingId), out QuestListingRecordData listing)) return false;
            snapshot = new QuestListingSnapshot(listing);
            return true;
        }

        public IReadOnlyList<QuestSourceSnapshot> QuerySources(QuestSourceQuery query = null)
        {
            QuestSourceQuery actual = query ?? new QuestSourceQuery();
            IEnumerable<QuestSourceRecordData> records = sourcesById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(value => string.Equals(value.worldId, actual.worldId, StringComparison.Ordinal));
            if (!actual.includeHistorical) records = records.Where(value => value.lifecycleState != QuestSourceLifecycleState.Retired && value.lifecycleState != QuestSourceLifecycleState.Historical && value.lifecycleState != QuestSourceLifecycleState.Invalid);
            if (!string.IsNullOrWhiteSpace(actual.questSourceId)) records = records.Where(value => string.Equals(value.questSourceId, actual.questSourceId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.definitionId)) records = records.Where(value => string.Equals(value.questSourceDefinitionId, actual.definitionId, StringComparison.Ordinal));
            if (actual.category.HasValue) records = records.Where(value => registry != null && registry.TryGet(value.questSourceDefinitionId, out QuestSourceDefinition definition) && definition.Category == actual.category.Value);
            if (!string.IsNullOrWhiteSpace(actual.locationId)) records = records.Where(value => string.Equals(value.hostLocationId, actual.locationId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.interactionPointId)) records = records.Where(value => string.Equals(value.interactionPointId, actual.interactionPointId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.organizationId)) records = records.Where(value => string.Equals(value.operatingOrganizationId, actual.organizationId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.governmentId)) records = records.Where(value => string.Equals(value.operatingGovernmentId, actual.governmentId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.factionId)) records = records.Where(value => string.Equals(value.operatingFactionId, actual.factionId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.officeId)) records = records.Where(value => string.Equals(value.operatingOfficeId, actual.officeId, StringComparison.Ordinal));
            records = records.Where(value => CanSee(value.visibility, actual.access, actual.requesterPersonId, value));
            return records.OrderBy(value => value.createdWorldTime).ThenBy(value => value.questSourceId, StringComparer.Ordinal).Select(value => new QuestSourceSnapshot(value)).ToArray();
        }

        public IReadOnlyList<QuestListingSnapshot> QueryListings(QuestListingQuery query = null)
        {
            QuestListingQuery actual = query ?? new QuestListingQuery();
            IEnumerable<QuestListingRecordData> records = listingsById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(value => string.Equals(value.worldId, actual.worldId, StringComparison.Ordinal));
            if (!actual.includeHistorical) records = records.Where(value => value.lifecycleState == QuestListingLifecycleState.Published);
            if (!string.IsNullOrWhiteSpace(actual.questListingId)) records = records.Where(value => string.Equals(value.questListingId, actual.questListingId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questSourceId)) records = records.Where(value => string.Equals(value.questSourceId, actual.questSourceId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(value => string.Equals(value.questId, actual.questId, StringComparison.Ordinal));
            records = records.Where(value =>
            {
                if (!sourcesById.TryGetValue(value.questSourceId, out QuestSourceRecordData source)) return false;
                bool visible = CanSee(source.visibility, actual.access, actual.requesterPersonId, source) && CanSee(value.visibility, actual.access, actual.requesterPersonId, source);
                return visible || (actual.includeHidden && actual.access == QuestVisibilityAccess.PrivilegedDiagnostic);
            });
            return records.OrderBy(value => value.priority).ThenBy(value => value.publishedWorldTime).ThenBy(value => value.questListingId, StringComparer.Ordinal).Select(value => new QuestListingSnapshot(value, ShouldRedact(value.visibility, actual.access))).ToArray();
        }

        public IReadOnlyList<QuestSourceDiscoveryRecordData> QueryDiscoveries(string personId = null, string questSourceId = null, string questListingId = null, string questId = null)
        {
            IEnumerable<QuestSourceDiscoveryRecordData> records = discoveriesById.Values;
            if (!string.IsNullOrWhiteSpace(personId)) records = records.Where(value => string.Equals(value.personId, personId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(questSourceId)) records = records.Where(value => string.Equals(value.questSourceId, questSourceId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(questListingId)) records = records.Where(value => string.Equals(value.questListingId, questListingId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(questId)) records = records.Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal));
            return records.OrderBy(value => value.worldTime).ThenBy(value => value.discoveryId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        }

        public IReadOnlyList<QuestSourceAssociationRecordData> QueryAssociations(QuestSourceRole? role = null, string questSourceId = null, string questListingId = null, string questId = null, string assignmentId = null)
        {
            IEnumerable<QuestSourceAssociationRecordData> records = associationsById.Values;
            if (role.HasValue) records = records.Where(value => value.role == role.Value);
            if (!string.IsNullOrWhiteSpace(questSourceId)) records = records.Where(value => string.Equals(value.questSourceId, questSourceId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(questListingId)) records = records.Where(value => string.Equals(value.questListingId, questListingId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(questId)) records = records.Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(assignmentId)) records = records.Where(value => string.Equals(value.assignmentId, assignmentId, StringComparison.Ordinal));
            return records.OrderBy(value => value.worldTime).ThenBy(value => value.associationId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        }

        public QuestSourceRuntimeSaveData CreateSaveData()
        {
            return new QuestSourceRuntimeSaveData
            {
                worldId = worldId,
                revision = revision,
                sources = sourcesById.Values.OrderBy(value => value.questSourceId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                listings = listingsById.Values.OrderBy(value => value.questListingId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                discoveries = discoveriesById.Values.OrderBy(value => value.discoveryId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                associations = associationsById.Values.OrderBy(value => value.associationId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                events = events.OrderBy(value => value.runtimeRevision).ThenBy(value => value.eventId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(value => value.transactionId, StringComparer.Ordinal).Select(value => value.Clone()).ToList()
            };
        }

        public QuestSourceOperationResult RestoreFromSaveData(QuestSourceRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry definitionRegistry, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (!ValidateSaveData(saveData, quests, participation, definitionRegistry ?? registry, expectedWorldId, out string failure))
            {
                return QuestSourceOperationResult.Failure(QuestSourceOperationStatus.PersistenceInvalid, failure, revision);
            }

            QuestSourceRuntimeSaveData rollback = CreateSaveData();
            try
            {
                Configure(quests, participation, definitionRegistry ?? registry, string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId);
                sourcesById.Clear();
                listingsById.Clear();
                discoveriesById.Clear();
                associationsById.Clear();
                transactionsById.Clear();
                events.Clear();
                foreach (QuestSourceRecordData source in saveData.sources ?? new List<QuestSourceRecordData>()) sourcesById[source.questSourceId] = source.Clone();
                foreach (QuestListingRecordData listing in saveData.listings ?? new List<QuestListingRecordData>()) listingsById[listing.questListingId] = listing.Clone();
                foreach (QuestSourceDiscoveryRecordData discovery in saveData.discoveries ?? new List<QuestSourceDiscoveryRecordData>()) discoveriesById[discovery.discoveryId] = discovery.Clone();
                foreach (QuestSourceAssociationRecordData association in saveData.associations ?? new List<QuestSourceAssociationRecordData>()) associationsById[association.associationId] = association.Clone();
                foreach (QuestSourceTransactionData transaction in saveData.transactions ?? new List<QuestSourceTransactionData>()) transactionsById[transaction.transactionId] = transaction.Clone();
                events.AddRange((saveData.events ?? new List<QuestSourceEventData>()).Select(value => value.Clone()));
                revision = saveData.revision;
                RebuildIndexes();
                return QuestSourceOperationResult.Success("Quest sources restored.", revision, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, questRuntime, participationRuntime, registry, worldId);
                return QuestSourceOperationResult.Failure(QuestSourceOperationStatus.RestoreFailed, $"Quest source restore failed: {exception.Message}", revision);
            }
        }

        public QuestSourceRuntimeValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), questRuntime, participationRuntime, registry, worldId, out _, out QuestSourceRuntimeValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(QuestSourceRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, quests, participation, registry, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(QuestSourceRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry, string expectedWorldId, out string failure, out QuestSourceRuntimeValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (saveData == null)
            {
                errors.Add("Quest source save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != QuestSourceRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported quest source save schema version {saveData.schemaVersion}.");
                string expected = string.IsNullOrWhiteSpace(expectedWorldId) ? saveData.worldId : expectedWorldId;
                if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(saveData.worldId, expected, StringComparison.Ordinal)) errors.Add($"Quest source save world '{saveData.worldId}' does not match expected world '{expected}'.");
                if (registry == null) errors.Add("Quest source save validation requires a definition registry.");
                if (quests == null) errors.Add("Quest source save validation requires QuestRuntime.");

                HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestSourceRecordData source in saveData.sources ?? new List<QuestSourceRecordData>())
                {
                    if (source == null) continue;
                    if (string.IsNullOrWhiteSpace(source.questSourceId)) errors.Add("Quest Source record has no source ID.");
                    else if (!sourceIds.Add(source.questSourceId)) errors.Add($"Duplicate Quest Source record '{source.questSourceId}'.");
                    if (registry != null && !registry.TryGet(source.questSourceDefinitionId, out QuestSourceDefinition _)) errors.Add($"Quest Source '{source.questSourceId}' references missing Quest Source definition '{source.questSourceDefinitionId}'.");
                    if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(source.worldId, expected, StringComparison.Ordinal)) errors.Add($"Quest Source '{source.questSourceId}' belongs to world '{source.worldId}', expected '{expected}'.");
                    if (source.lifecycleState == QuestSourceLifecycleState.Unknown) errors.Add($"Quest Source '{source.questSourceId}' has unknown lifecycle state.");
                    if (source.visibility == QuestSourceVisibility.Unknown) errors.Add($"Quest Source '{source.questSourceId}' has unknown visibility.");
                }

                HashSet<string> listingIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestListingRecordData listing in saveData.listings ?? new List<QuestListingRecordData>())
                {
                    if (listing == null) continue;
                    if (string.IsNullOrWhiteSpace(listing.questListingId)) errors.Add("Quest Listing record has no listing ID.");
                    else if (!listingIds.Add(listing.questListingId)) errors.Add($"Duplicate Quest Listing record '{listing.questListingId}'.");
                    if (!sourceIds.Contains(listing.questSourceId)) errors.Add($"Quest Listing '{listing.questListingId}' references missing Quest Source '{listing.questSourceId}'.");
                    if (quests != null && !quests.TryGetSnapshot(listing.questId, out _)) errors.Add($"Quest Listing '{listing.questListingId}' references missing Quest '{listing.questId}'.");
                    if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(listing.worldId, expected, StringComparison.Ordinal)) errors.Add($"Quest Listing '{listing.questListingId}' belongs to world '{listing.worldId}', expected '{expected}'.");
                    if (listing.lifecycleState == QuestListingLifecycleState.Unknown) errors.Add($"Quest Listing '{listing.questListingId}' has unknown lifecycle state.");
                    if (listing.expirationWorldTime >= 0d && listing.expirationWorldTime < listing.publishedWorldTime) errors.Add($"Quest Listing '{listing.questListingId}' expires before publication.");
                }

                HashSet<string> discoveryIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestSourceDiscoveryRecordData discovery in saveData.discoveries ?? new List<QuestSourceDiscoveryRecordData>())
                {
                    if (discovery == null) continue;
                    if (string.IsNullOrWhiteSpace(discovery.discoveryId)) errors.Add("Quest Source discovery has no discovery ID.");
                    else if (!discoveryIds.Add(discovery.discoveryId)) errors.Add($"Duplicate Quest Source discovery '{discovery.discoveryId}'.");
                    if (string.IsNullOrWhiteSpace(discovery.personId)) errors.Add($"Quest Source discovery '{discovery.discoveryId}' has no Person ID.");
                    if (!string.IsNullOrWhiteSpace(discovery.questSourceId) && !sourceIds.Contains(discovery.questSourceId)) errors.Add($"Quest Source discovery '{discovery.discoveryId}' references missing Quest Source '{discovery.questSourceId}'.");
                    if (!string.IsNullOrWhiteSpace(discovery.questListingId) && !listingIds.Contains(discovery.questListingId)) errors.Add($"Quest Source discovery '{discovery.discoveryId}' references missing Quest Listing '{discovery.questListingId}'.");
                    if (!string.IsNullOrWhiteSpace(discovery.questId) && quests != null && !quests.TryGetSnapshot(discovery.questId, out _)) errors.Add($"Quest Source discovery '{discovery.discoveryId}' references missing Quest '{discovery.questId}'.");
                    if (discovery.discoveryKind == QuestSourceDiscoveryKind.Unknown) errors.Add($"Quest Source discovery '{discovery.discoveryId}' has unknown discovery kind.");
                }

                HashSet<string> associationIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestSourceAssociationRecordData association in saveData.associations ?? new List<QuestSourceAssociationRecordData>())
                {
                    if (association == null) continue;
                    if (string.IsNullOrWhiteSpace(association.associationId)) errors.Add("Quest Source association has no association ID.");
                    else if (!associationIds.Add(association.associationId)) errors.Add($"Duplicate Quest Source association '{association.associationId}'.");
                    if (association.role == QuestSourceRole.Unknown) errors.Add($"Quest Source association '{association.associationId}' has unknown role.");
                    if (!string.IsNullOrWhiteSpace(association.questSourceId) && !sourceIds.Contains(association.questSourceId)) errors.Add($"Quest Source association '{association.associationId}' references missing Quest Source '{association.questSourceId}'.");
                    if (!string.IsNullOrWhiteSpace(association.questListingId) && !listingIds.Contains(association.questListingId)) errors.Add($"Quest Source association '{association.associationId}' references missing Quest Listing '{association.questListingId}'.");
                    if (!string.IsNullOrWhiteSpace(association.questId) && quests != null && !quests.TryGetSnapshot(association.questId, out _)) errors.Add($"Quest Source association '{association.associationId}' references missing Quest '{association.questId}'.");
                    if (!string.IsNullOrWhiteSpace(association.assignmentId) && participation != null && !participation.TryGetAssignment(association.assignmentId, out _)) errors.Add($"Quest Source association '{association.associationId}' references missing Quest Assignment '{association.assignmentId}'.");
                }

                HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestSourceEventData sourceEvent in saveData.events ?? new List<QuestSourceEventData>())
                {
                    if (sourceEvent == null) continue;
                    if (string.IsNullOrWhiteSpace(sourceEvent.eventId)) errors.Add("Quest Source event has no event ID.");
                    else if (!eventIds.Add(sourceEvent.eventId)) errors.Add($"Duplicate Quest Source event '{sourceEvent.eventId}'.");
                    if (sourceEvent.eventKind == QuestSourceEventKind.Unknown) errors.Add($"Quest Source event '{sourceEvent.eventId}' has unknown kind.");
                }

                HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestSourceTransactionData transaction in saveData.transactions ?? new List<QuestSourceTransactionData>())
                {
                    if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId)) errors.Add("Quest Source transaction is missing an ID.");
                    else if (!transactionIds.Add(transaction.transactionId)) errors.Add($"Duplicate Quest Source transaction '{transaction.transactionId}'.");
                }
            }

            report = new QuestSourceRuntimeValidationReport(errors, warnings);
            failure = errors.Count == 0 ? string.Empty : string.Join(" | ", errors);
            return errors.Count == 0;
        }

        public void Dispose()
        {
            disposed = true;
        }

        private IEnumerable<QuestVisibleListingSnapshot> BuildVisibleListings(QuestSourceRecordData source, QuestSourceBrowseRequest request)
        {
            if (!listingsBySource.TryGetValue(source.questSourceId, out List<string> ids)) yield break;
            foreach (string id in ids)
            {
                if (!listingsById.TryGetValue(id, out QuestListingRecordData listing)) continue;
                if (!request.includeHistorical && !IsVisibleForActiveBrowse(listing)) continue;
                if (!CanSee(listing.visibility, request.access, request.requesterPersonId, source)) continue;
                if (questRuntime == null || !questRuntime.TryGetSnapshot(listing.questId, out QuestSnapshot quest)) continue;
                if (!CanSee(ToSourceVisibility(quest.Visibility), request.access, request.requesterPersonId, source)) continue;
                if (!string.IsNullOrWhiteSpace(request.categoryFilterTag) && !quest.TagIds.Contains(request.categoryFilterTag, StringComparer.Ordinal)) continue;

                QuestEligibilityContext eligibilityContext = request.eligibilityContext?.Clone() ?? new QuestEligibilityContext { personId = request.requesterPersonId, worldTime = request.worldTime };
                eligibilityContext.personId = string.IsNullOrWhiteSpace(eligibilityContext.personId) ? request.requesterPersonId : eligibilityContext.personId;
                eligibilityContext.locationId = string.IsNullOrWhiteSpace(eligibilityContext.locationId) ? source.hostLocationId : eligibilityContext.locationId;
                eligibilityContext.interactionPointId = string.IsNullOrWhiteSpace(eligibilityContext.interactionPointId) ? source.interactionPointId : eligibilityContext.interactionPointId;
                QuestAvailabilityResult availability = participationRuntime?.EvaluateAvailability(quest.QuestId, eligibilityContext);
                QuestEligibilityResult eligibility = participationRuntime?.EvaluateEligibility(quest.QuestId, eligibilityContext);
                bool eligible = eligibility?.Eligible ?? request.access == QuestVisibilityAccess.PrivilegedDiagnostic;
                bool ineligibleReasonRedacted = false;
                QuestEligibilityDisplayPolicy displayPolicy = listing.eligibilityDisplayPolicy == QuestEligibilityDisplayPolicy.Unknown ? QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason : listing.eligibilityDisplayPolicy;
                if (!eligible)
                {
                    if (displayPolicy == QuestEligibilityDisplayPolicy.OnlyEligible && !request.includeIneligible) continue;
                    if (displayPolicy == QuestEligibilityDisplayPolicy.OnlyEligible) continue;
                    ineligibleReasonRedacted = displayPolicy == QuestEligibilityDisplayPolicy.VisibleIneligibleRedacted || eligibility?.HiddenFailureCount > 0;
                }

                bool taken = participationRuntime != null && participationRuntime.QueryAssignments(new QuestAssignmentQuery { questId = quest.QuestId, includeHistorical = false, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Any();
                if (taken && listing.acceptedDisplayPolicy == QuestAcceptedListingDisplayPolicy.HideWhenAccepted && listing.lifecycleState == QuestListingLifecycleState.Claimed && !request.includeHistorical) continue;

                yield return new QuestVisibleListingSnapshot(new QuestListingSnapshot(listing, ShouldRedact(listing.visibility, request.access)), ShouldRedact(ToSourceVisibility(quest.Visibility), request.access) ? null : quest, availability, eligibility, ineligibleReasonRedacted, taken);
            }
        }

        private static bool IsVisibleForActiveBrowse(QuestListingRecordData listing)
        {
            if (listing == null)
            {
                return false;
            }

            if (listing.lifecycleState == QuestListingLifecycleState.Published)
            {
                return true;
            }

            return listing.lifecycleState == QuestListingLifecycleState.Claimed
                && (listing.acceptedDisplayPolicy == QuestAcceptedListingDisplayPolicy.ShowAsTaken
                    || listing.acceptedDisplayPolicy == QuestAcceptedListingDisplayPolicy.KeepVisible);
        }

        private QuestOfferChannel OfferChannelFromSource(QuestSourceRecordData source)
        {
            if (source == null || registry == null || !registry.TryGet(source.questSourceDefinitionId, out QuestSourceDefinition definition))
            {
                return QuestOfferChannel.DirectInstitution;
            }

            return definition.Category switch
            {
                QuestSourceCategory.QuestBoard or QuestSourceCategory.PublicNotice => QuestOfferChannel.QuestBoard,
                QuestSourceCategory.GuildCounter or QuestSourceCategory.Organization or QuestSourceCategory.Business => QuestOfferChannel.GuildCounter,
                QuestSourceCategory.GovernmentDesk or QuestSourceCategory.Office => QuestOfferChannel.GovernmentDesk,
                QuestSourceCategory.NPC => QuestOfferChannel.DirectPerson,
                QuestSourceCategory.Record => QuestOfferChannel.Record,
                QuestSourceCategory.Letter => QuestOfferChannel.Letter,
                QuestSourceCategory.TravelEncounter => QuestOfferChannel.TravelEncounter,
                QuestSourceCategory.WorldEvent => QuestOfferChannel.NarrativeEvent,
                QuestSourceCategory.System => QuestOfferChannel.SystemGenerated,
                _ => QuestOfferChannel.DirectInstitution
            };
        }

        private bool CanPublish(QuestSourceDefinition sourceDefinition, QuestSnapshot quest, QuestDefinition questDefinition, QuestListingPublishRequest request, out QuestSourceOperationStatus status, out string failure)
        {
            status = QuestSourceOperationStatus.Succeeded;
            failure = string.Empty;
            string[] suppliedAuthority = QuestRuntimeModelUtility.Clean((request.publisherAuthorityIds ?? Array.Empty<string>()).Concat(new[] { request.publisherAuthorityId }));
            foreach (string required in sourceDefinition.PublicationAuthorityRequirementIds)
            {
                if (!suppliedAuthority.Contains(required, StringComparer.Ordinal))
                {
                    status = QuestSourceOperationStatus.UnauthorizedPublisher;
                    failure = $"Publishing to source definition '{sourceDefinition.Id}' requires authority '{required}'.";
                    return false;
                }
            }

            QuestSourceFilterData filters = sourceDefinition.Filters;
            if (filters.allowedQuestCategories.Length > 0 && !filters.allowedQuestCategories.Contains(questDefinition.Category))
            {
                status = QuestSourceOperationStatus.SourceFilterRejected;
                failure = $"Quest category '{questDefinition.Category}' is not allowed by source definition '{sourceDefinition.Id}'.";
                return false;
            }

            foreach (string tag in filters.requiredQuestTagIds)
            {
                if (!quest.TagIds.Contains(tag, StringComparer.Ordinal))
                {
                    status = QuestSourceOperationStatus.SourceFilterRejected;
                    failure = $"Quest '{quest.QuestId}' is missing required listing tag '{tag}'.";
                    return false;
                }
            }

            if (filters.allowedIssuerIds.Length > 0 && !filters.allowedIssuerIds.Contains(quest.Issuer.issuerId, StringComparer.Ordinal))
            {
                status = QuestSourceOperationStatus.SourceFilterRejected;
                failure = $"Quest issuer '{quest.Issuer.issuerId}' is not allowed by source definition '{sourceDefinition.Id}'.";
                return false;
            }

            if (filters.allowedRepeatabilityPolicies.Length > 0 && !filters.allowedRepeatabilityPolicies.Contains(questDefinition.RepeatabilityPolicy))
            {
                status = QuestSourceOperationStatus.SourceFilterRejected;
                failure = $"Quest repeatability '{questDefinition.RepeatabilityPolicy}' is not allowed by source definition '{sourceDefinition.Id}'.";
                return false;
            }

            return true;
        }

        private IEnumerable<QuestListingRecordData> ActiveListingsForSource(string sourceId)
        {
            if (!listingsBySource.TryGetValue(N(sourceId), out List<string> ids)) return Array.Empty<QuestListingRecordData>();
            return ids.Select(id => listingsById.TryGetValue(id, out QuestListingRecordData listing) ? listing : null).Where(value => value != null && value.lifecycleState == QuestListingLifecycleState.Published).ToArray();
        }

        private bool ValidateRevision(long expectedRevision, out QuestSourceOperationResult failure)
        {
            failure = null;
            if (expectedRevision >= 0L && expectedRevision != revision)
            {
                failure = Fail(QuestSourceOperationStatus.RevisionConflict, $"Expected revision {expectedRevision}, actual {revision}.");
                return false;
            }

            return true;
        }

        private bool TryDuplicate(string transactionId, out QuestSourceOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!transactionsById.TryGetValue(transactionId, out QuestSourceTransactionData transaction)) return false;
            QuestSourceRecordData source = !string.IsNullOrWhiteSpace(transaction.questSourceId) && sourcesById.TryGetValue(transaction.questSourceId, out QuestSourceRecordData foundSource) ? foundSource : null;
            QuestListingRecordData listing = !string.IsNullOrWhiteSpace(transaction.questListingId) && listingsById.TryGetValue(transaction.questListingId, out QuestListingRecordData foundListing) ? foundListing : null;
            result = QuestSourceOperationResult.Success("Duplicate Quest Source transaction ignored.", revision, revision, source, listing, duplicate: true);
            return true;
        }

        private void RecordTransaction(string transactionId, string operation, string sourceId, string listingId, string questId)
        {
            transactionId = N(transactionId);
            if (string.IsNullOrWhiteSpace(transactionId) || transactionsById.ContainsKey(transactionId)) return;
            transactionsById[transactionId] = new QuestSourceTransactionData
            {
                transactionId = transactionId,
                operation = operation ?? string.Empty,
                questSourceId = N(sourceId),
                questListingId = N(listingId),
                questId = N(questId),
                runtimeRevision = revision
            };
        }

        private void RecordEvent(string transactionId, QuestSourceEventKind kind, string sourceId, string listingId, string questId, QuestSourceLifecycleState sourceBefore, QuestSourceLifecycleState sourceAfter, QuestListingLifecycleState listingBefore, QuestListingLifecycleState listingAfter, double worldTime, string provenanceId)
        {
            events.Add(new QuestSourceEventData
            {
                eventId = $"quest-source-event.{revision:000000}.{events.Count + 1:0000}",
                transactionId = N(transactionId),
                eventKind = kind,
                questSourceId = N(sourceId),
                questListingId = N(listingId),
                questId = N(questId),
                sourceBeforeState = sourceBefore,
                sourceAfterState = sourceAfter,
                listingBeforeState = listingBefore,
                listingAfterState = listingAfter,
                worldTime = worldTime,
                provenanceId = N(provenanceId),
                runtimeRevision = revision
            });
        }

        private void RebuildIndexes()
        {
            listingsBySource.Clear();
            listingsByQuest.Clear();
            sourcesByInteractionPoint.Clear();
            sourcesByLocation.Clear();
            foreach (QuestSourceRecordData source in sourcesById.Values)
            {
                AddIndex(sourcesByInteractionPoint, source.interactionPointId, source.questSourceId);
                AddIndex(sourcesByLocation, source.hostLocationId, source.questSourceId);
            }

            foreach (QuestListingRecordData listing in listingsById.Values)
            {
                AddIndex(listingsBySource, listing.questSourceId, listing.questListingId);
                AddIndex(listingsByQuest, listing.questId, listing.questListingId);
            }
        }

        private static void AddIndex(IDictionary<string, List<string>> index, string key, string value)
        {
            key = N(key);
            value = N(value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }

            if (!values.Contains(value, StringComparer.Ordinal)) values.Add(value);
        }

        private QuestSourceOperationResult Fail(QuestSourceOperationStatus status, string message)
        {
            return QuestSourceOperationResult.Failure(status, message, revision);
        }

        private static bool IsActive(QuestSourceLifecycleState state)
        {
            return state == QuestSourceLifecycleState.Active;
        }

        private static bool IsActive(QuestListingLifecycleState state)
        {
            return state == QuestListingLifecycleState.Published;
        }

        private static bool CanSee(QuestSourceVisibility visibility, QuestVisibilityAccess access, string requesterPersonId, QuestSourceRecordData source)
        {
            if (access == QuestVisibilityAccess.PrivilegedDiagnostic) return true;
            switch (visibility)
            {
                case QuestSourceVisibility.Public:
                    return true;
                case QuestSourceVisibility.LocallyKnown:
                    return access >= QuestVisibilityAccess.LocalKnowledge;
                case QuestSourceVisibility.OrganizationMembers:
                case QuestSourceVisibility.RankRestricted:
                    return access >= QuestVisibilityAccess.OrganizationMember;
                case QuestSourceVisibility.GovernmentOfficial:
                    return access >= QuestVisibilityAccess.Government;
                case QuestSourceVisibility.RecipientOnly:
                    return access >= QuestVisibilityAccess.Recipient && !string.IsNullOrWhiteSpace(requesterPersonId);
                case QuestSourceVisibility.Restricted:
                    return access >= QuestVisibilityAccess.OrganizationMember || access >= QuestVisibilityAccess.Government;
                case QuestSourceVisibility.FactionKnown:
                case QuestSourceVisibility.Secret:
                case QuestSourceVisibility.Hidden:
                case QuestSourceVisibility.Diagnostic:
                    return false;
                default:
                    return false;
            }
        }

        private static InformationSubjectReferenceData SourceSubject(string questSourceId)
        {
            questSourceId = N(questSourceId);
            return QuestInformationSubject.Source(questSourceId, string.Empty, questSourceId);
        }

        private static string SourceControllingEntity(QuestSourceRecordData source)
        {
            if (source == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(source.operatingOfficeId)) return source.operatingOfficeId;
            if (!string.IsNullOrWhiteSpace(source.operatingOrganizationId)) return source.operatingOrganizationId;
            if (!string.IsNullOrWhiteSpace(source.operatingGovernmentId)) return source.operatingGovernmentId;
            if (!string.IsNullOrWhiteSpace(source.operatingFactionId)) return source.operatingFactionId;
            if (!string.IsNullOrWhiteSpace(source.operatingBusinessId)) return source.operatingBusinessId;
            return source.questSourceId ?? string.Empty;
        }

        private static QuestIssuerReferenceData IssuerFromSource(QuestSourceRecordData source)
        {
            if (source == null)
            {
                return new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" };
            }

            if (!string.IsNullOrWhiteSpace(source.operatingOfficeId))
            {
                return new QuestIssuerReferenceData { issuerType = QuestIssuerType.Office, issuerId = source.operatingOfficeId };
            }

            if (!string.IsNullOrWhiteSpace(source.operatingOrganizationId))
            {
                return new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = source.operatingOrganizationId };
            }

            if (!string.IsNullOrWhiteSpace(source.operatingGovernmentId))
            {
                return new QuestIssuerReferenceData { issuerType = QuestIssuerType.Government, issuerId = source.operatingGovernmentId };
            }

            if (!string.IsNullOrWhiteSpace(source.operatingFactionId))
            {
                return new QuestIssuerReferenceData { issuerType = QuestIssuerType.Faction, issuerId = source.operatingFactionId };
            }

            if (!string.IsNullOrWhiteSpace(source.operatingBusinessId))
            {
                return new QuestIssuerReferenceData { issuerType = QuestIssuerType.Business, issuerId = source.operatingBusinessId };
            }

            return new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" };
        }

        private static bool ShouldRedact(QuestSourceVisibility visibility, QuestVisibilityAccess access)
        {
            if (access == QuestVisibilityAccess.PrivilegedDiagnostic) return false;
            return visibility == QuestSourceVisibility.Restricted || visibility == QuestSourceVisibility.Secret || visibility == QuestSourceVisibility.Hidden || visibility == QuestSourceVisibility.Diagnostic;
        }

        private static QuestSourceVisibility ToSourceVisibility(QuestVisibility visibility)
        {
            return visibility switch
            {
                QuestVisibility.Public => QuestSourceVisibility.Public,
                QuestVisibility.LocallyKnown => QuestSourceVisibility.LocallyKnown,
                QuestVisibility.OrganizationKnown or QuestVisibility.MemberKnown => QuestSourceVisibility.OrganizationMembers,
                QuestVisibility.GovernmentKnown => QuestSourceVisibility.GovernmentOfficial,
                QuestVisibility.RecipientKnown => QuestSourceVisibility.RecipientOnly,
                QuestVisibility.Restricted => QuestSourceVisibility.Restricted,
                QuestVisibility.Secret => QuestSourceVisibility.Secret,
                QuestVisibility.Hidden => QuestSourceVisibility.Hidden,
                QuestVisibility.Diagnostic or QuestVisibility.Development => QuestSourceVisibility.Diagnostic,
                _ => QuestSourceVisibility.Hidden
            };
        }

        private static QuestVisibility ToQuestVisibility(QuestSourceVisibility visibility)
        {
            return visibility switch
            {
                QuestSourceVisibility.Public => QuestVisibility.Public,
                QuestSourceVisibility.LocallyKnown => QuestVisibility.LocallyKnown,
                QuestSourceVisibility.OrganizationMembers or QuestSourceVisibility.RankRestricted => QuestVisibility.MemberKnown,
                QuestSourceVisibility.GovernmentOfficial => QuestVisibility.GovernmentKnown,
                QuestSourceVisibility.RecipientOnly => QuestVisibility.RecipientKnown,
                QuestSourceVisibility.Restricted => QuestVisibility.Restricted,
                QuestSourceVisibility.Secret => QuestVisibility.Secret,
                QuestSourceVisibility.Hidden => QuestVisibility.Hidden,
                QuestSourceVisibility.Diagnostic => QuestVisibility.Diagnostic,
                _ => QuestVisibility.Hidden
            };
        }

        private static QuestSourceEventKind EventFromListingState(QuestListingLifecycleState state)
        {
            return state switch
            {
                QuestListingLifecycleState.Suspended => QuestSourceEventKind.ListingSuspended,
                QuestListingLifecycleState.Unlisted => QuestSourceEventKind.ListingUnlisted,
                QuestListingLifecycleState.Expired => QuestSourceEventKind.ListingExpired,
                QuestListingLifecycleState.Claimed => QuestSourceEventKind.ListingClaimed,
                _ => QuestSourceEventKind.ListingUnlisted
            };
        }

        private static string BuildSourceId(string definitionId, string locationId, string interactionPointId)
        {
            string suffix = !string.IsNullOrWhiteSpace(interactionPointId) ? interactionPointId : !string.IsNullOrWhiteSpace(locationId) ? locationId : Guid.NewGuid().ToString("N");
            return $"quest-source.{Sanitize(definitionId)}.{Sanitize(suffix)}";
        }

        private static string BuildListingId(string sourceId, string questId)
        {
            return $"quest-listing.{Sanitize(sourceId)}.{Sanitize(questId)}";
        }

        private static string BuildDiscoveryId(string personId, string sourceId, string listingId, string questId, QuestSourceDiscoveryKind kind)
        {
            return $"quest-source-discovery.{Sanitize(personId)}.{Sanitize(sourceId)}.{Sanitize(listingId)}.{Sanitize(questId)}.{kind.ToString().ToLowerInvariant()}";
        }

        private static string BuildAssociationId(QuestSourceRole role, string sourceId, string listingId, string questId, string assignmentId, string outcomeId, string rewardId)
        {
            return $"quest-source-association.{role.ToString().ToLowerInvariant()}.{Sanitize(sourceId)}.{Sanitize(listingId)}.{Sanitize(questId)}.{Sanitize(assignmentId)}.{Sanitize(outcomeId)}.{Sanitize(rewardId)}";
        }

        private static string Sanitize(string value)
        {
            value = N(value);
            if (string.IsNullOrWhiteSpace(value)) return "none";
            char[] chars = value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            return new string(chars).Trim('-');
        }

        private static string N(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
