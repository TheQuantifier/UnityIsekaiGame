using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Quests
{
    public sealed class QuestParticipationRuntime : IDisposable
    {
        private readonly Dictionary<string, QuestOfferRecordData> offersById = new Dictionary<string, QuestOfferRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestAssignmentRecordData> assignmentsById = new Dictionary<string, QuestAssignmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestParticipationTransactionData> transactionsById = new Dictionary<string, QuestParticipationTransactionData>(StringComparer.Ordinal);
        private readonly List<QuestParticipationEventData> events = new List<QuestParticipationEventData>();

        private DefinitionRegistry registry;
        private QuestRuntime questRuntime;
        private string worldId;
        private long revision;
        private bool disposed;

        public QuestParticipationRuntime(QuestRuntime quests = null, DefinitionRegistry definitionRegistry = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(quests, definitionRegistry, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int OfferCount => offersById.Count;
        public int AssignmentCount => assignmentsById.Count;
        public IReadOnlyList<QuestParticipationEventData> Events => events.Select(value => value.Clone()).ToArray();

        public void Configure(QuestRuntime quests, DefinitionRegistry definitionRegistry, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            questRuntime = quests;
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
        }

        public QuestAvailabilityResult EvaluateAvailability(string questId, QuestEligibilityContext context = null)
        {
            context ??= new QuestEligibilityContext();
            if (!TryResolveQuest(questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure))
            {
                return new QuestAvailabilityResult(N(questId), QuestAvailabilityState.Invalid, false, 0, context.privilegedDiagnostics ? 0 : -1, new[] { failure }, questRuntime?.Revision ?? 0L, revision);
            }

            List<string> reasons = new List<string>();
            QuestAvailabilityState state = QuestAvailabilityState.Available;
            double time = context.worldTime;
            if (quest.LifecycleState == QuestRuntimeLifecycleState.Suspended)
            {
                state = QuestAvailabilityState.Suspended;
                reasons.Add("quest.suspended");
            }
            else if (quest.LifecycleState == QuestRuntimeLifecycleState.Unavailable)
            {
                state = QuestAvailabilityState.TemporarilyUnavailable;
                reasons.Add("quest.unavailable");
            }
            else if (quest.LifecycleState == QuestRuntimeLifecycleState.Retired)
            {
                state = QuestAvailabilityState.Retired;
                reasons.Add("quest.retired");
            }
            else if (quest.LifecycleState == QuestRuntimeLifecycleState.Historical)
            {
                state = QuestAvailabilityState.Historical;
                reasons.Add("quest.historical");
            }
            else if (quest.LifecycleState == QuestRuntimeLifecycleState.Invalid || quest.LifecycleState == QuestRuntimeLifecycleState.Unknown || quest.LifecycleState == QuestRuntimeLifecycleState.Draft)
            {
                state = QuestAvailabilityState.Invalid;
                reasons.Add("quest.invalid-lifecycle");
            }

            if (state == QuestAvailabilityState.Available && definition.AvailabilityStartWorldTime >= 0d && time < definition.AvailabilityStartWorldTime)
            {
                state = QuestAvailabilityState.NotYetAvailable;
                reasons.Add("availability.not-yet-started");
            }

            if (state == QuestAvailabilityState.Available && definition.AvailabilityEndWorldTime >= 0d && time > definition.AvailabilityEndWorldTime)
            {
                state = QuestAvailabilityState.Exhausted;
                reasons.Add("availability.expired");
            }

            int active = ActiveAssignmentsForQuest(quest.QuestId).Count();
            int capacity = definition.AssignmentCapacity;
            if (state == QuestAvailabilityState.Available && definition.AssignmentPolicy == QuestAssignmentPolicy.Exclusive && active > 0)
            {
                state = QuestAvailabilityState.ExclusivelyAssigned;
                reasons.Add("assignment.exclusive-claimed");
            }
            else if (state == QuestAvailabilityState.Available && capacity > 0 && active >= capacity)
            {
                state = QuestAvailabilityState.Exhausted;
                reasons.Add("assignment.capacity-exhausted");
            }

            int visibleActive = context.privilegedDiagnostics ? active : -1;
            return new QuestAvailabilityResult(quest.QuestId, state, state == QuestAvailabilityState.Available, capacity, visibleActive, reasons, quest.Revision, revision);
        }

        public QuestEligibilityResult EvaluateEligibility(string questId, QuestEligibilityContext context)
        {
            context = (context ?? new QuestEligibilityContext()).Clone();
            QuestAvailabilityResult availability = EvaluateAvailability(questId, context);
            List<string> visible = new List<string>();
            int hidden = 0;
            if (!availability.Available)
            {
                visible.AddRange(availability.Reasons);
            }

            if (string.IsNullOrWhiteSpace(context.personId))
            {
                visible.Add("person.missing");
            }

            if (TryResolveQuest(questId, out QuestSnapshot quest, out QuestDefinition definition, out _))
            {
                EvaluateRecipient(quest, context, visible, ref hidden);
                int undertakingSize = definition.RequireReadyPartyAtAcceptance ? context.ReadyPartySize : context.PartySize;
                if (undertakingSize < definition.RequiredPartySize)
                {
                    visible.Add($"Requires at least {definition.RequiredPartySize} ready party member{(definition.RequiredPartySize == 1 ? string.Empty : "s")} (current: {undertakingSize}).");
                }
                foreach (QuestEligibilityRequirementGroupData group in definition.EligibilityRequirementGroups)
                {
                    EvaluateGroup(group, context, visible, ref hidden);
                }
            }

            bool eligible = visible.Count == 0 && hidden == 0;
            return new QuestEligibilityResult(N(questId), N(context.personId), availability, eligible, visible, hidden, (questRuntime?.Revision ?? 0L) + revision);
        }

        public QuestParticipationOperationResult CreateOffer(QuestOfferRequest request)
        {
            if (disposed) return Fail(QuestParticipationOperationStatus.Disposed, "Quest participation runtime is disposed.");
            request ??= new QuestOfferRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestParticipationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestParticipationOperationResult duplicate)) return duplicate;
            if (!TryResolveQuest(request.questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)) return Fail(QuestParticipationOperationStatus.MissingQuest, failure);

            QuestEligibilityContext context = request.eligibilityContext?.Clone() ?? new QuestEligibilityContext { worldTime = request.worldTime, privilegedDiagnostics = true };
            QuestAvailabilityResult availability = EvaluateAvailability(quest.QuestId, context);
            if (!availability.Available) return Fail(QuestParticipationOperationStatus.Unavailable, "Quest is not currently offerable.", availability: availability);
            if (!ProviderAuthorized(definition, request.offeringProvider, request.authorityBasisId, context, out string authorityFailure)) return Fail(QuestParticipationOperationStatus.UnauthorizedProvider, authorityFailure, availability: availability);
            QuestEligibilityResult eligibility = null;
            if (definition.PrevalidateEligibilityForOffers && request.recipient?.recipientScope == QuestRecipientScope.Person)
            {
                context.personId = request.recipient.recipientId;
                eligibility = EvaluateEligibility(quest.QuestId, context);
                if (!eligibility.Eligible) return Fail(QuestParticipationOperationStatus.Ineligible, "Recipient is not eligible for this quest offer.", availability: availability, eligibility: eligibility);
            }

            QuestRecipientReferenceData recipient = request.recipient?.Clone() ?? new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open };
            if (HasActiveDuplicateOffer(quest.QuestId, recipient) && definition.RefusalPolicy != QuestRefusalPolicy.MayReoffer)
            {
                return Fail(QuestParticipationOperationStatus.DuplicateOffer, "An active offer already exists for this quest and recipient.", availability: availability, eligibility: eligibility);
            }

            QuestOfferRecordData offer = new QuestOfferRecordData
            {
                offerId = string.IsNullOrWhiteSpace(request.offerId) ? BuildOfferId(quest.QuestId, recipient, offersById.Count + 1) : N(request.offerId),
                questId = quest.QuestId,
                worldId = worldId,
                recipient = recipient,
                institutionalIssuer = request.institutionalIssuer?.Clone() ?? quest.Issuer,
                offeringProvider = request.offeringProvider?.Clone() ?? quest.Issuer,
                channel = request.channel == QuestOfferChannel.Unknown ? ChannelFromQuest(quest) : request.channel,
                sourceInteractionPointId = N(string.IsNullOrWhiteSpace(request.sourceInteractionPointId) ? quest.Origin.interactionPointId : request.sourceInteractionPointId),
                sourceLocationId = N(string.IsNullOrWhiteSpace(request.sourceLocationId) ? quest.Origin.locationId : request.sourceLocationId),
                createdWorldTime = request.worldTime,
                expirationWorldTime = request.expirationWorldTime >= 0d ? request.expirationWorldTime : definition.DefaultOfferDuration >= 0d ? request.worldTime + definition.DefaultOfferDuration : -1d,
                lifecycleState = QuestOfferLifecycleState.Active,
                visibility = request.visibility ?? quest.Visibility,
                authorityBasisId = N(request.authorityBasisId),
                eligibilityFingerprint = eligibility?.Fingerprint ?? string.Empty,
                revision = 1L
            };

            if (offersById.ContainsKey(offer.offerId)) return Fail(QuestParticipationOperationStatus.DuplicateOffer, $"Quest offer '{offer.offerId}' already exists.", availability: availability, eligibility: eligibility);
            if (request.preview) return Success("Quest offer previewed.", offer: offer, availability: availability, eligibility: eligibility, preview: true);

            long before = revision;
            offersById[offer.offerId] = offer.Clone();
            revision++;
            RecordTransaction(transactionId, "CreateOffer", quest.QuestId, offer.offerId, string.Empty);
            RecordEvent(transactionId, QuestParticipationEventKind.OfferCreated, quest.QuestId, offer.offerId, string.Empty, recipient.recipientId, request.worldTime);
            return QuestParticipationOperationResult.Success("Quest offer created.", before, revision, offer: new QuestOfferSnapshot(offer), availability: availability, eligibility: eligibility);
        }

        public QuestParticipationOperationResult AcceptOffer(QuestAcceptOfferRequest request)
        {
            if (disposed) return Fail(QuestParticipationOperationStatus.Disposed, "Quest participation runtime is disposed.");
            request ??= new QuestAcceptOfferRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestParticipationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestParticipationOperationResult duplicate)) return duplicate;
            string offerId = N(request.offerId);
            if (!offersById.TryGetValue(offerId, out QuestOfferRecordData offer)) return Fail(QuestParticipationOperationStatus.MissingOffer, $"Quest offer '{offerId}' is missing.");
            if (offer.lifecycleState != QuestOfferLifecycleState.Active) return Fail(QuestParticipationOperationStatus.OfferNotActive, $"Quest offer '{offerId}' is not active.");

            string personId = N(string.IsNullOrWhiteSpace(request.personId) ? offer.recipient?.recipientId : request.personId);
            if (offer.expirationWorldTime >= 0d && request.worldTime > offer.expirationWorldTime) return Fail(QuestParticipationOperationStatus.OfferExpired, "Quest offer has expired.");
            if (offer.recipient?.recipientScope == QuestRecipientScope.Person && !string.Equals(offer.recipient.recipientId, personId, StringComparison.Ordinal)) return Fail(QuestParticipationOperationStatus.Ineligible, "Quest offer is addressed to another Person.");
            if (!TryResolveQuest(offer.questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)) return Fail(QuestParticipationOperationStatus.MissingQuest, failure);
            if (definition.ConsentPolicy == QuestConsentPolicy.ExplicitRecipientConsentRequired && !request.explicitConsent) return Fail(QuestParticipationOperationStatus.ConsentRequired, "Quest acceptance requires explicit recipient consent.");

            QuestEligibilityContext context = request.eligibilityContext?.Clone() ?? new QuestEligibilityContext { worldTime = request.worldTime, privilegedDiagnostics = true };
            context.personId = personId;
            QuestAvailabilityResult availability = EvaluateAvailability(quest.QuestId, context);
            if (!availability.Available) return Fail(QuestParticipationOperationStatus.Unavailable, "Acceptance revalidation found the quest unavailable.", availability: availability);
            QuestEligibilityResult eligibility = EvaluateEligibility(quest.QuestId, context);
            if (!eligibility.Eligible) return Fail(QuestParticipationOperationStatus.Ineligible, "Acceptance revalidation failed.", eligibility: eligibility, availability: eligibility.Availability);

            if (HasActiveAssignmentForPerson(quest.QuestId, personId)) return Fail(QuestParticipationOperationStatus.DuplicateAssignment, "Person already has an active assignment for this quest.", eligibility: eligibility, availability: eligibility.Availability);
            if (!HasCapacity(definition, quest.QuestId, out QuestParticipationOperationStatus capacityStatus, out string capacityFailure)) return Fail(capacityStatus, capacityFailure, eligibility: eligibility, availability: eligibility.Availability);

            QuestOfferRecordData changedOffer = offer.Clone();
            changedOffer.lifecycleState = QuestOfferLifecycleState.Accepted;
            changedOffer.revision++;
            QuestAssignmentRecordData assignment = CreateAssignmentRecord(request.assignmentId, quest, definition, changedOffer, personId, changedOffer.offeringProvider, QuestAssignmentCategory.AcceptedOffer, request.explicitConsent ? request.consentRecordId : string.Empty, request.authorityBasisId, request.worldTime, changedOffer.visibility, context);
            if (assignmentsById.ContainsKey(assignment.assignmentId)) return Fail(QuestParticipationOperationStatus.DuplicateAssignment, $"Quest assignment '{assignment.assignmentId}' already exists.", eligibility: eligibility, availability: eligibility.Availability);
            if (request.preview) return Success("Quest offer acceptance previewed.", offer: changedOffer, assignment: assignment, eligibility: eligibility, availability: eligibility.Availability, preview: true);

            long before = revision;
            offersById[changedOffer.offerId] = changedOffer;
            assignmentsById[assignment.assignmentId] = assignment.Clone();
            revision++;
            RecordTransaction(transactionId, "AcceptOffer", quest.QuestId, changedOffer.offerId, assignment.assignmentId);
            RecordEvent(transactionId, QuestParticipationEventKind.OfferAccepted, quest.QuestId, changedOffer.offerId, assignment.assignmentId, personId, request.worldTime);
            RecordEvent(transactionId, QuestParticipationEventKind.AssignmentCreated, quest.QuestId, changedOffer.offerId, assignment.assignmentId, personId, request.worldTime);
            return QuestParticipationOperationResult.Success("Quest offer accepted and assignment created.", before, revision, new QuestOfferSnapshot(changedOffer), new QuestAssignmentSnapshot(assignment), eligibility.Availability, eligibility);
        }

        public QuestParticipationOperationResult DirectAssign(QuestDirectAssignmentRequest request)
        {
            if (disposed) return Fail(QuestParticipationOperationStatus.Disposed, "Quest participation runtime is disposed.");
            request ??= new QuestDirectAssignmentRequest();
            if (!ValidateRevision(request.expectedRevision, out QuestParticipationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestParticipationOperationResult duplicate)) return duplicate;
            if (!TryResolveQuest(request.questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)) return Fail(QuestParticipationOperationStatus.MissingQuest, failure);
            if (definition.ConsentPolicy != QuestConsentPolicy.DirectInstitutionalAssignmentAllowed && !request.explicitConsent) return Fail(QuestParticipationOperationStatus.ConsentRequired, "Direct assignment requires explicit consent unless the definition allows institutional assignment.");

            QuestEligibilityContext context = request.eligibilityContext?.Clone() ?? new QuestEligibilityContext { worldTime = request.worldTime, privilegedDiagnostics = true };
            context.personId = request.assigneePersonId;
            QuestAvailabilityResult availability = EvaluateAvailability(quest.QuestId, context);
            if (!availability.Available) return Fail(QuestParticipationOperationStatus.Unavailable, "Direct assignment found the quest unavailable.", availability: availability);
            QuestEligibilityResult eligibility = EvaluateEligibility(quest.QuestId, context);
            if (!eligibility.Eligible) return Fail(QuestParticipationOperationStatus.Ineligible, "Direct assignment eligibility failed.", eligibility: eligibility, availability: eligibility.Availability);
            if (!ProviderAuthorized(definition, request.assignedBy, request.authorityBasisId, context, out string authorityFailure)) return Fail(QuestParticipationOperationStatus.UnauthorizedProvider, authorityFailure, eligibility: eligibility, availability: eligibility.Availability);
            if (HasActiveAssignmentForPerson(quest.QuestId, request.assigneePersonId)) return Fail(QuestParticipationOperationStatus.DuplicateAssignment, "Person already has an active assignment for this quest.", eligibility: eligibility, availability: eligibility.Availability);
            if (!HasCapacity(definition, quest.QuestId, out QuestParticipationOperationStatus capacityStatus, out string capacityFailure)) return Fail(capacityStatus, capacityFailure, eligibility: eligibility, availability: eligibility.Availability);

            QuestAssignmentRecordData assignment = CreateAssignmentRecord(request.assignmentId, quest, definition, null, request.assigneePersonId, request.assignedBy, QuestAssignmentCategory.DirectInstitutional, request.explicitConsent ? request.consentRecordId : string.Empty, request.authorityBasisId, request.worldTime, request.visibility ?? quest.Visibility, context);
            assignment.institutionalIssuer = request.institutionalIssuer?.Clone() ?? quest.Issuer;
            if (assignmentsById.ContainsKey(assignment.assignmentId)) return Fail(QuestParticipationOperationStatus.DuplicateAssignment, $"Quest assignment '{assignment.assignmentId}' already exists.", eligibility: eligibility, availability: eligibility.Availability);
            if (request.preview) return Success("Quest direct assignment previewed.", assignment: assignment, eligibility: eligibility, availability: eligibility.Availability, preview: true);

            long before = revision;
            assignmentsById[assignment.assignmentId] = assignment.Clone();
            revision++;
            RecordTransaction(transactionId, "DirectAssign", quest.QuestId, string.Empty, assignment.assignmentId);
            RecordEvent(transactionId, QuestParticipationEventKind.AssignmentCreated, quest.QuestId, string.Empty, assignment.assignmentId, assignment.assigneePersonId, request.worldTime);
            return QuestParticipationOperationResult.Success("Quest direct assignment created.", before, revision, assignment: new QuestAssignmentSnapshot(assignment), availability: eligibility.Availability, eligibility: eligibility);
        }

        public QuestParticipationOperationResult RefuseOffer(QuestOfferLifecycleRequest request) => TransitionOffer(request, QuestOfferLifecycleState.Refused, QuestParticipationEventKind.OfferRefused);
        public QuestParticipationOperationResult WithdrawOffer(QuestOfferLifecycleRequest request) => TransitionOffer(request, QuestOfferLifecycleState.Withdrawn, QuestParticipationEventKind.OfferWithdrawn);
        public QuestParticipationOperationResult ExpireOffer(QuestOfferLifecycleRequest request) => TransitionOffer(request, QuestOfferLifecycleState.Expired, QuestParticipationEventKind.OfferExpired);
        public QuestParticipationOperationResult AbandonAssignment(QuestAssignmentLifecycleRequest request) => TransitionAssignment(request, QuestAssignmentLifecycleState.Abandoned, QuestParticipationEventKind.AssignmentAbandoned);
        public QuestParticipationOperationResult WithdrawAssignment(QuestAssignmentLifecycleRequest request) => TransitionAssignment(request, QuestAssignmentLifecycleState.Withdrawn, QuestParticipationEventKind.AssignmentWithdrawn);
        public QuestParticipationOperationResult SuspendAssignment(QuestAssignmentLifecycleRequest request) => TransitionAssignment(request, QuestAssignmentLifecycleState.Suspended, QuestParticipationEventKind.AssignmentSuspended);
        public QuestParticipationOperationResult ResumeAssignment(QuestAssignmentLifecycleRequest request) => TransitionAssignment(request, QuestAssignmentLifecycleState.Active, QuestParticipationEventKind.AssignmentResumed);

        public IReadOnlyList<QuestParticipationOperationResult> ExpireOffers(double worldTime, string transactionPrefix = "tx.quest-offer.expire")
        {
            return offersById.Values
                .Where(offer => offer.lifecycleState == QuestOfferLifecycleState.Active && offer.expirationWorldTime >= 0d && worldTime >= offer.expirationWorldTime)
                .OrderBy(offer => offer.expirationWorldTime)
                .ThenBy(offer => offer.offerId, StringComparer.Ordinal)
                .Select(offer => ExpireOffer(new QuestOfferLifecycleRequest { transactionId = $"{transactionPrefix}.{offer.offerId}", offerId = offer.offerId, targetState = QuestOfferLifecycleState.Expired, worldTime = worldTime }))
                .ToArray();
        }

        public bool TryGetOffer(string offerId, out QuestOfferSnapshot snapshot)
        {
            snapshot = null;
            if (!offersById.TryGetValue(N(offerId), out QuestOfferRecordData offer)) return false;
            snapshot = new QuestOfferSnapshot(offer);
            return true;
        }

        public bool TryGetAssignment(string assignmentId, out QuestAssignmentSnapshot snapshot)
        {
            snapshot = null;
            if (!assignmentsById.TryGetValue(N(assignmentId), out QuestAssignmentRecordData assignment)) return false;
            snapshot = new QuestAssignmentSnapshot(assignment);
            return true;
        }

        public IReadOnlyList<QuestOfferSnapshot> QueryOffers(QuestOfferQuery query = null)
        {
            QuestOfferQuery actual = query ?? new QuestOfferQuery();
            IEnumerable<QuestOfferRecordData> records = offersById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(record => string.Equals(record.worldId, actual.worldId, StringComparison.Ordinal));
            if (!actual.includeHistorical) records = records.Where(record => record.lifecycleState == QuestOfferLifecycleState.Active || record.lifecycleState == QuestOfferLifecycleState.Proposed);
            if (!string.IsNullOrWhiteSpace(actual.offerId)) records = records.Where(record => string.Equals(record.offerId, actual.offerId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(record => string.Equals(record.questId, actual.questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.recipientPersonId)) records = records.Where(record => string.Equals(record.recipient?.recipientId, actual.recipientPersonId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.issuerId)) records = records.Where(record => string.Equals(record.institutionalIssuer?.issuerId, actual.issuerId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.providerPersonId)) records = records.Where(record => string.Equals(record.offeringProvider?.actingPersonId, actual.providerPersonId, StringComparison.Ordinal) || string.Equals(record.offeringProvider?.issuerId, actual.providerPersonId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.interactionPointId)) records = records.Where(record => string.Equals(record.sourceInteractionPointId, actual.interactionPointId, StringComparison.Ordinal));
            if (actual.lifecycleState.HasValue) records = records.Where(record => record.lifecycleState == actual.lifecycleState.Value);
            records = records.Where(record => CanSee(record.visibility, actual.access, actual.requesterPersonId, record.recipient?.recipientId));
            return records.OrderBy(record => record.createdWorldTime).ThenBy(record => record.offerId, StringComparer.Ordinal).Select(record => new QuestOfferSnapshot(record)).ToArray();
        }

        public IReadOnlyList<QuestAssignmentSnapshot> QueryAssignments(QuestAssignmentQuery query = null)
        {
            QuestAssignmentQuery actual = query ?? new QuestAssignmentQuery();
            IEnumerable<QuestAssignmentRecordData> records = assignmentsById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(record => string.Equals(record.worldId, actual.worldId, StringComparison.Ordinal));
            if (!actual.includeHistorical) records = records.Where(IsActiveAssignment);
            if (!string.IsNullOrWhiteSpace(actual.assignmentId)) records = records.Where(record => string.Equals(record.assignmentId, actual.assignmentId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(record => string.Equals(record.questId, actual.questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.assigneePersonId)) records = records.Where(record => string.Equals(record.assigneePersonId, actual.assigneePersonId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.participantPersonId)) records = records.Where(record => QuestAssignmentRecordData.CleanParticipants(record.participantPersonIds, record.assigneePersonId).Contains(actual.participantPersonId, StringComparer.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.issuerId)) records = records.Where(record => string.Equals(record.institutionalIssuer?.issuerId, actual.issuerId, StringComparison.Ordinal));
            if (actual.lifecycleState.HasValue) records = records.Where(record => record.lifecycleState == actual.lifecycleState.Value);
            records = records.Where(record => CanSee(record.visibility, actual.access, actual.requesterPersonId, record.assigneePersonId));
            return records.OrderBy(record => record.assignedWorldTime).ThenBy(record => record.assignmentId, StringComparer.Ordinal).Select(record => new QuestAssignmentSnapshot(record)).ToArray();
        }

        public QuestParticipationSummary SummarizeQuestParticipation(string questId, QuestVisibilityAccess access, string requesterPersonId = null)
        {
            bool privileged = access == QuestVisibilityAccess.PrivilegedDiagnostic;
            int offerCount = privileged ? offersById.Values.Count(offer => offer.questId == N(questId)) : QueryOffers(new QuestOfferQuery { questId = questId, access = access, requesterPersonId = requesterPersonId, includeHistorical = true }).Count;
            int assignmentCount = privileged ? assignmentsById.Values.Count(assignment => assignment.questId == N(questId)) : QueryAssignments(new QuestAssignmentQuery { questId = questId, access = access, requesterPersonId = requesterPersonId, includeHistorical = true }).Count;
            return new QuestParticipationSummary(questId, privileged ? offerCount : offerCount, privileged ? assignmentCount : assignmentCount, !privileged);
        }

        public QuestParticipationRuntimeSaveData CreateSaveData()
        {
            return new QuestParticipationRuntimeSaveData
            {
                worldId = worldId,
                revision = revision,
                offers = offersById.Values.OrderBy(record => record.offerId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                assignments = assignmentsById.Values.OrderBy(record => record.assignmentId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                events = events.OrderBy(record => record.runtimeRevision).ThenBy(record => record.eventId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(record => record.transactionId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public QuestParticipationOperationResult RestoreFromSaveData(QuestParticipationRuntimeSaveData saveData, QuestRuntime quests, DefinitionRegistry definitionRegistry, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (!ValidateSaveData(saveData, quests ?? questRuntime, definitionRegistry ?? registry, expectedWorldId, out string failure))
            {
                return Fail(QuestParticipationOperationStatus.PersistenceInvalid, failure);
            }

            QuestParticipationRuntimeSaveData rollback = CreateSaveData();
            try
            {
                Configure(quests ?? questRuntime, definitionRegistry ?? registry, string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId);
                offersById.Clear();
                assignmentsById.Clear();
                transactionsById.Clear();
                events.Clear();
                foreach (QuestOfferRecordData offer in saveData.offers ?? new List<QuestOfferRecordData>()) offersById[offer.offerId] = offer.Clone();
                foreach (QuestAssignmentRecordData assignment in saveData.assignments ?? new List<QuestAssignmentRecordData>()) assignmentsById[assignment.assignmentId] = assignment.Clone();
                foreach (QuestParticipationTransactionData transaction in saveData.transactions ?? new List<QuestParticipationTransactionData>()) transactionsById[transaction.transactionId] = transaction.Clone();
                events.AddRange((saveData.events ?? new List<QuestParticipationEventData>()).Select(value => value.Clone()));
                revision = saveData.revision;
                return QuestParticipationOperationResult.Success("Quest participation restored.", revision, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, questRuntime, registry, worldId);
                return Fail(QuestParticipationOperationStatus.RestoreFailed, $"Quest participation restore failed: {exception.Message}");
            }
        }

        public QuestParticipationValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), questRuntime, registry, worldId, out _, out QuestParticipationValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(QuestParticipationRuntimeSaveData saveData, QuestRuntime quests, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, quests, registry, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(QuestParticipationRuntimeSaveData saveData, QuestRuntime quests, DefinitionRegistry registry, string expectedWorldId, out string failure, out QuestParticipationValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (saveData == null)
            {
                errors.Add("Quest participation save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != QuestParticipationRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported quest participation save schema version {saveData.schemaVersion}.");
                string world = string.IsNullOrWhiteSpace(expectedWorldId) ? saveData.worldId : expectedWorldId;
                if (!string.IsNullOrWhiteSpace(world) && !string.Equals(saveData.worldId, world, StringComparison.Ordinal)) errors.Add($"Quest participation save world '{saveData.worldId}' does not match expected world '{world}'.");
                if (quests == null) errors.Add("Quest participation validation requires QuestRuntime.");
                if (registry == null) errors.Add("Quest participation validation requires DefinitionRegistry.");

                HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> assignmentIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestOfferRecordData offer in saveData.offers ?? new List<QuestOfferRecordData>())
                {
                    ValidateOffer(offer, quests, registry, offerIds, errors);
                }

                foreach (QuestAssignmentRecordData assignment in saveData.assignments ?? new List<QuestAssignmentRecordData>())
                {
                    ValidateAssignment(assignment, quests, registry, offerIds, assignmentIds, errors);
                }

                HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestParticipationTransactionData transaction in saveData.transactions ?? new List<QuestParticipationTransactionData>())
                {
                    if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId)) errors.Add("Quest participation transaction is missing an ID.");
                    else if (!transactionIds.Add(transaction.transactionId)) errors.Add($"Duplicate quest participation transaction ID '{transaction.transactionId}'.");
                }
            }

            report = new QuestParticipationValidationReport(errors, warnings);
            failure = report.Succeeded ? string.Empty : string.Join(" | ", report.Errors);
            return report.Succeeded;
        }

        public void Clear()
        {
            offersById.Clear();
            assignmentsById.Clear();
            transactionsById.Clear();
            events.Clear();
            revision = 0L;
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
        }

        private QuestParticipationOperationResult TransitionOffer(QuestOfferLifecycleRequest request, QuestOfferLifecycleState target, QuestParticipationEventKind kind)
        {
            if (disposed) return Fail(QuestParticipationOperationStatus.Disposed, "Quest participation runtime is disposed.");
            request ??= new QuestOfferLifecycleRequest();
            request.targetState = target;
            if (!ValidateRevision(request.expectedRevision, out QuestParticipationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestParticipationOperationResult duplicate)) return duplicate;
            string offerId = N(request.offerId);
            if (!offersById.TryGetValue(offerId, out QuestOfferRecordData offer)) return Fail(QuestParticipationOperationStatus.MissingOffer, $"Quest offer '{offerId}' is missing.");
            if (offer.lifecycleState != QuestOfferLifecycleState.Active && offer.lifecycleState != QuestOfferLifecycleState.Proposed) return Fail(QuestParticipationOperationStatus.OfferNotActive, $"Quest offer '{offerId}' is not active.");
            if (!TryResolveQuest(offer.questId, out QuestSnapshot quest, out QuestDefinition definition, out _)) return Fail(QuestParticipationOperationStatus.MissingQuest, "Quest for offer is missing.");
            if (target == QuestOfferLifecycleState.Withdrawn && !definition.IssuerWithdrawalAllowed) return Fail(QuestParticipationOperationStatus.WithdrawalNotAllowed, "Quest definition does not allow issuer offer withdrawal.");

            QuestOfferRecordData changed = offer.Clone();
            changed.lifecycleState = target;
            changed.revision++;
            if (request.preview) return Success("Quest offer transition previewed.", offer: changed, preview: true);

            long before = revision;
            offersById[offerId] = changed;
            revision++;
            RecordTransaction(transactionId, target.ToString(), offer.questId, offer.offerId, string.Empty);
            RecordEvent(transactionId, kind, offer.questId, offer.offerId, string.Empty, offer.recipient?.recipientId, request.worldTime);
            return QuestParticipationOperationResult.Success("Quest offer transitioned.", before, revision, offer: new QuestOfferSnapshot(changed));
        }

        private QuestParticipationOperationResult TransitionAssignment(QuestAssignmentLifecycleRequest request, QuestAssignmentLifecycleState target, QuestParticipationEventKind kind)
        {
            if (disposed) return Fail(QuestParticipationOperationStatus.Disposed, "Quest participation runtime is disposed.");
            request ??= new QuestAssignmentLifecycleRequest();
            request.targetState = target;
            if (!ValidateRevision(request.expectedRevision, out QuestParticipationOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestParticipationOperationResult duplicate)) return duplicate;
            string assignmentId = N(request.assignmentId);
            if (!assignmentsById.TryGetValue(assignmentId, out QuestAssignmentRecordData assignment)) return Fail(QuestParticipationOperationStatus.MissingAssignment, $"Quest assignment '{assignmentId}' is missing.");
            if (!TryResolveQuest(assignment.questId, out QuestSnapshot quest, out QuestDefinition definition, out _)) return Fail(QuestParticipationOperationStatus.MissingQuest, "Quest for assignment is missing.");
            if (target == QuestAssignmentLifecycleState.Abandoned && definition.AbandonmentPolicy == QuestAbandonmentPolicy.NotAllowed) return Fail(QuestParticipationOperationStatus.AbandonmentNotAllowed, "Quest definition does not allow abandonment.");
            if (target == QuestAssignmentLifecycleState.Withdrawn && !definition.IssuerWithdrawalAllowed) return Fail(QuestParticipationOperationStatus.WithdrawalNotAllowed, "Quest definition does not allow issuer assignment withdrawal.");
            if (target == QuestAssignmentLifecycleState.Abandoned && !request.explicitConsent && string.Equals(assignment.assigneePersonId, request.actingPersonId, StringComparison.Ordinal)) return Fail(QuestParticipationOperationStatus.ConsentRequired, "Abandonment requires explicit assignee consent.");

            QuestAssignmentRecordData changed = assignment.Clone();
            changed.lifecycleState = target;
            if (target == QuestAssignmentLifecycleState.Abandoned || target == QuestAssignmentLifecycleState.Withdrawn || target == QuestAssignmentLifecycleState.Historical || target == QuestAssignmentLifecycleState.Invalid)
            {
                changed.endedWorldTime = request.worldTime;
            }

            changed.authorityBasisId = string.IsNullOrWhiteSpace(request.authorityBasisId) ? changed.authorityBasisId : request.authorityBasisId;
            changed.revision++;
            if (request.preview) return Success("Quest assignment transition previewed.", assignment: changed, preview: true);

            long before = revision;
            assignmentsById[assignmentId] = changed;
            revision++;
            RecordTransaction(transactionId, target.ToString(), assignment.questId, assignment.offerId, assignment.assignmentId);
            RecordEvent(transactionId, kind, assignment.questId, assignment.offerId, assignment.assignmentId, assignment.assigneePersonId, request.worldTime);
            return QuestParticipationOperationResult.Success("Quest assignment transitioned.", before, revision, assignment: new QuestAssignmentSnapshot(changed));
        }

        private void EvaluateRecipient(QuestSnapshot quest, QuestEligibilityContext context, List<string> visible, ref int hidden)
        {
            QuestRecipientReferenceData recipient = quest.IntendedRecipient;
            string id = N(recipient.recipientId);
            bool ok = recipient.recipientScope switch
            {
                QuestRecipientScope.Open => true,
                QuestRecipientScope.Person => string.Equals(id, N(context.personId), StringComparison.Ordinal),
                QuestRecipientScope.OrganizationMembers => context.facts.Contains(QuestEligibilityRequirementKind.OrganizationMembership, id),
                QuestRecipientScope.OrganizationRank => context.facts.Contains(QuestEligibilityRequirementKind.OrganizationRank, id),
                QuestRecipientScope.Officeholder => context.facts.Contains(QuestEligibilityRequirementKind.Office, id),
                QuestRecipientScope.Profession => context.facts.Contains(QuestEligibilityRequirementKind.Profession, id),
                QuestRecipientScope.FactionMembers => context.facts.Contains(QuestEligibilityRequirementKind.FactionAffiliation, id),
                QuestRecipientScope.Citizens => context.facts.Contains(QuestEligibilityRequirementKind.Citizenship, id),
                QuestRecipientScope.Party => context.PartySize > 1 && (string.IsNullOrWhiteSpace(id) || string.Equals(id, N(context.partyId), StringComparison.Ordinal)),
                QuestRecipientScope.MultiplePersons => context.PartySize > 1,
                QuestRecipientScope.Custom => context.facts.Contains(QuestEligibilityRequirementKind.Custom, id),
                _ => false
            };

            if (!ok) visible.Add($"recipient.{recipient.recipientScope}.mismatch");
        }

        private void EvaluateGroup(QuestEligibilityRequirementGroupData group, QuestEligibilityContext context, List<string> visible, ref int hidden)
        {
            QuestEligibilityRequirementData[] requirements = (group.requirements ?? Array.Empty<QuestEligibilityRequirementData>()).Where(value => value != null).ToArray();
            bool[] results = requirements.Select(requirement => EvaluateRequirement(requirement, context)).ToArray();
            int passed = results.Count(value => value);
            bool groupPassed = group.policy switch
            {
                QuestEligibilityGroupPolicy.Any => passed > 0,
                QuestEligibilityGroupPolicy.None => passed == 0,
                QuestEligibilityGroupPolicy.AtLeast => passed >= Math.Max(1, group.thresholdCount),
                _ => passed == requirements.Length
            };

            if (groupPassed) return;
            foreach (QuestEligibilityRequirementData requirement in requirements.Where((_, index) => !results[index]))
            {
                if (group.revealFailures && requirement.revealFailure) visible.Add($"requirement.{N(requirement.requirementId)}.{requirement.kind}.missing");
                else hidden++;
            }
        }

        private static bool EvaluateRequirement(QuestEligibilityRequirementData requirement, QuestEligibilityContext context)
        {
            if (requirement.kind == QuestEligibilityRequirementKind.Location)
            {
                bool match = string.Equals(N(context.locationId), N(requirement.requiredId), StringComparison.Ordinal);
                return requirement.negate ? !match : match;
            }

            if (requirement.kind == QuestEligibilityRequirementKind.InteractionPointPresence)
            {
                bool match = string.Equals(N(context.interactionPointId), N(requirement.requiredId), StringComparison.Ordinal);
                return requirement.negate ? !match : match;
            }

            bool result;
            if (requirement.comparison == QuestRequirementComparison.GreaterThanOrEqual || requirement.comparison == QuestRequirementComparison.LessThanOrEqual || requirement.comparison == QuestRequirementComparison.Equal)
            {
                int value = context.facts.Value(requirement.kind, requirement.requiredId);
                result = requirement.comparison switch
                {
                    QuestRequirementComparison.LessThanOrEqual => value != int.MinValue && value <= requirement.maximumValue,
                    QuestRequirementComparison.Equal => value != int.MinValue && value == requirement.minimumValue,
                    _ => value != int.MinValue && value >= requirement.minimumValue
                };
            }
            else
            {
                result = context.facts.Contains(requirement.kind, requirement.requiredId);
                if (requirement.comparison == QuestRequirementComparison.NotExists) result = !result;
            }

            return requirement.negate ? !result : result;
        }

        private bool TryResolveQuest(string questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)
        {
            quest = null;
            definition = null;
            if (questRuntime == null)
            {
                failure = "QuestRuntime is missing.";
                return false;
            }

            if (registry == null)
            {
                failure = "DefinitionRegistry is missing.";
                return false;
            }

            if (!questRuntime.TryGetSnapshot(N(questId), out quest))
            {
                failure = $"Quest '{N(questId)}' is missing.";
                return false;
            }

            if (!registry.TryGet(quest.QuestDefinitionId, out definition))
            {
                failure = $"Quest definition '{quest.QuestDefinitionId}' is missing.";
                return false;
            }

            if (!string.Equals(quest.WorldId, worldId, StringComparison.Ordinal))
            {
                failure = $"Quest world '{quest.WorldId}' does not match participation runtime world '{worldId}'.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private bool ProviderAuthorized(QuestDefinition definition, QuestIssuerReferenceData provider, string authorityBasisId, QuestEligibilityContext context, out string failure)
        {
            provider ??= new QuestIssuerReferenceData { issuerType = QuestIssuerType.System };
            if (provider.issuerType == QuestIssuerType.System)
            {
                failure = string.Empty;
                return true;
            }

            string[] requirements = definition.OfferingAuthorityRequirementIds.ToArray();
            if (requirements.Length == 0)
            {
                failure = string.Empty;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(authorityBasisId) && requirements.Contains(N(authorityBasisId), StringComparer.Ordinal))
            {
                failure = string.Empty;
                return true;
            }

            bool ok = requirements.Any(id => context.facts.Contains(QuestEligibilityRequirementKind.InstitutionalAuthority, id));
            failure = ok ? string.Empty : "Offering provider lacks required quest authority.";
            return ok;
        }

        private bool HasCapacity(QuestDefinition definition, string questId, out QuestParticipationOperationStatus status, out string failure)
        {
            int active = ActiveAssignmentsForQuest(questId).Count();
            if (definition.AssignmentPolicy == QuestAssignmentPolicy.Exclusive && active > 0)
            {
                status = QuestParticipationOperationStatus.ExclusiveAssignmentExists;
                failure = "Quest already has an exclusive active assignment.";
                return false;
            }

            if (definition.AssignmentCapacity > 0 && active >= definition.AssignmentCapacity)
            {
                status = QuestParticipationOperationStatus.CapacityExceeded;
                failure = "Quest assignment capacity is exhausted.";
                return false;
            }

            status = QuestParticipationOperationStatus.Succeeded;
            failure = string.Empty;
            return true;
        }

        private IEnumerable<QuestAssignmentRecordData> ActiveAssignmentsForQuest(string questId)
        {
            return assignmentsById.Values.Where(assignment => string.Equals(assignment.questId, N(questId), StringComparison.Ordinal) && IsActiveAssignment(assignment));
        }

        private bool HasActiveAssignmentForPerson(string questId, string personId)
        {
            return ActiveAssignmentsForQuest(questId).Any(assignment => string.Equals(assignment.assigneePersonId, N(personId), StringComparison.Ordinal));
        }

        private bool HasActiveDuplicateOffer(string questId, QuestRecipientReferenceData recipient)
        {
            string stable = recipient?.StableKey ?? string.Empty;
            return offersById.Values.Any(offer => string.Equals(offer.questId, N(questId), StringComparison.Ordinal) && offer.lifecycleState == QuestOfferLifecycleState.Active && string.Equals(offer.recipient?.StableKey, stable, StringComparison.Ordinal));
        }

        private QuestAssignmentRecordData CreateAssignmentRecord(string assignmentId, QuestSnapshot quest, QuestDefinition definition, QuestOfferRecordData offer, string personId, QuestIssuerReferenceData assignedBy, QuestAssignmentCategory category, string consentRecordId, string authorityBasisId, double worldTime, QuestVisibility visibility, QuestEligibilityContext context)
        {
            string id = string.IsNullOrWhiteSpace(assignmentId) ? BuildAssignmentId(quest.QuestId, personId, assignmentsById.Count + 1) : N(assignmentId);
            return new QuestAssignmentRecordData
            {
                assignmentId = id,
                questId = quest.QuestId,
                offerId = offer?.offerId ?? string.Empty,
                worldId = worldId,
                assigneePersonId = N(personId),
                undertakingPartyId = N(context?.partyId),
                participantPersonIds = QuestAssignmentRecordData.CleanParticipants(definition.RequireReadyPartyAtAcceptance ? context?.readyPartyMemberPersonIds : context?.partyMemberPersonIds, personId),
                requiredPartySizeAtAcceptance = definition?.RequiredPartySize ?? 1,
                institutionalIssuer = offer?.institutionalIssuer?.Clone() ?? quest.Issuer,
                assignedBy = assignedBy?.Clone() ?? quest.Issuer,
                lifecycleState = QuestAssignmentLifecycleState.Active,
                category = category,
                assignedWorldTime = worldTime,
                visibility = visibility,
                consentRecordId = N(consentRecordId),
                authorityBasisId = N(authorityBasisId),
                revision = 1L
            };
        }

        private QuestParticipationOperationResult Success(string message, QuestOfferRecordData offer = null, QuestAssignmentRecordData assignment = null, QuestAvailabilityResult availability = null, QuestEligibilityResult eligibility = null, bool preview = false)
        {
            return QuestParticipationOperationResult.Success(message, revision, revision, offer == null ? null : new QuestOfferSnapshot(offer), assignment == null ? null : new QuestAssignmentSnapshot(assignment), availability, eligibility, preview: preview);
        }

        private QuestParticipationOperationResult Fail(QuestParticipationOperationStatus status, string message, QuestAvailabilityResult availability = null, QuestEligibilityResult eligibility = null)
        {
            return QuestParticipationOperationResult.Failure(status, message, revision, availability, eligibility);
        }

        private bool ValidateRevision(long expectedRevision, out QuestParticipationOperationResult result)
        {
            if (expectedRevision >= 0L && expectedRevision != revision)
            {
                result = Fail(QuestParticipationOperationStatus.RevisionConflict, $"Expected revision {expectedRevision}, actual {revision}.");
                return false;
            }

            result = null;
            return true;
        }

        private bool TryDuplicate(string transactionId, out QuestParticipationOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out QuestParticipationTransactionData transaction)) return false;
            QuestOfferSnapshot offer = TryGetOffer(transaction.offerId, out QuestOfferSnapshot foundOffer) ? foundOffer : null;
            QuestAssignmentSnapshot assignment = TryGetAssignment(transaction.assignmentId, out QuestAssignmentSnapshot foundAssignment) ? foundAssignment : null;
            result = QuestParticipationOperationResult.Success("Duplicate quest participation transaction ignored.", revision, revision, offer, assignment, duplicate: true);
            return true;
        }

        private void RecordTransaction(string transactionId, string operation, string questId, string offerId, string assignmentId)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new QuestParticipationTransactionData { transactionId = transactionId, operation = operation, questId = questId, offerId = offerId, assignmentId = assignmentId, runtimeRevision = revision };
        }

        private void RecordEvent(string transactionId, QuestParticipationEventKind kind, string questId, string offerId, string assignmentId, string personId, double worldTime)
        {
            events.Add(new QuestParticipationEventData
            {
                eventId = $"quest-participation-event.{revision:000000}.{kind}.{events.Count:000}",
                transactionId = transactionId ?? string.Empty,
                questId = questId ?? string.Empty,
                offerId = offerId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                eventKind = kind,
                worldTime = worldTime,
                runtimeRevision = revision
            });
        }

        private static void ValidateOffer(QuestOfferRecordData offer, QuestRuntime quests, DefinitionRegistry registry, ISet<string> offerIds, ICollection<string> errors)
        {
            if (offer == null) { errors.Add("Quest offer is null."); return; }
            if (string.IsNullOrWhiteSpace(offer.offerId)) errors.Add("Quest offer is missing an offer ID.");
            else if (!offerIds.Add(offer.offerId)) errors.Add($"Duplicate quest offer ID '{offer.offerId}'.");
            if (string.IsNullOrWhiteSpace(offer.questId) || quests == null || !quests.TryGetSnapshot(offer.questId, out QuestSnapshot quest)) errors.Add($"Quest offer '{offer.offerId}' references missing quest '{offer.questId}'.");
            else if (registry != null && !registry.TryGet(quest.QuestDefinitionId, out QuestDefinition _)) errors.Add($"Quest offer '{offer.offerId}' references quest with missing definition '{quest.QuestDefinitionId}'.");
            if (string.IsNullOrWhiteSpace(offer.worldId)) errors.Add($"Quest offer '{offer.offerId}' is missing world ID.");
            if (offer.lifecycleState == QuestOfferLifecycleState.Unknown) errors.Add($"Quest offer '{offer.offerId}' has unknown lifecycle state.");
            if (offer.visibility == QuestVisibility.Unknown) errors.Add($"Quest offer '{offer.offerId}' has unknown visibility.");
            if (offer.expirationWorldTime >= 0d && offer.expirationWorldTime < offer.createdWorldTime) errors.Add($"Quest offer '{offer.offerId}' expires before creation.");
        }

        private static void ValidateAssignment(QuestAssignmentRecordData assignment, QuestRuntime quests, DefinitionRegistry registry, ISet<string> offerIds, ISet<string> assignmentIds, ICollection<string> errors)
        {
            if (assignment == null) { errors.Add("Quest assignment is null."); return; }
            if (string.IsNullOrWhiteSpace(assignment.assignmentId)) errors.Add("Quest assignment is missing an assignment ID.");
            else if (!assignmentIds.Add(assignment.assignmentId)) errors.Add($"Duplicate quest assignment ID '{assignment.assignmentId}'.");
            if (string.IsNullOrWhiteSpace(assignment.questId) || quests == null || !quests.TryGetSnapshot(assignment.questId, out QuestSnapshot quest)) errors.Add($"Quest assignment '{assignment.assignmentId}' references missing quest '{assignment.questId}'.");
            else if (registry != null && !registry.TryGet(quest.QuestDefinitionId, out QuestDefinition _)) errors.Add($"Quest assignment '{assignment.assignmentId}' references quest with missing definition '{quest.QuestDefinitionId}'.");
            if (!string.IsNullOrWhiteSpace(assignment.offerId) && !offerIds.Contains(assignment.offerId)) errors.Add($"Quest assignment '{assignment.assignmentId}' references missing offer '{assignment.offerId}'.");
            if (string.IsNullOrWhiteSpace(assignment.assigneePersonId)) errors.Add($"Quest assignment '{assignment.assignmentId}' is missing assignee Person ID.");
            string[] participants = QuestAssignmentRecordData.CleanParticipants(assignment.participantPersonIds, assignment.assigneePersonId);
            if (!string.IsNullOrWhiteSpace(assignment.assigneePersonId) && !participants.Contains(assignment.assigneePersonId, StringComparer.Ordinal)) errors.Add($"Quest assignment '{assignment.assignmentId}' participant snapshot does not contain its assignee.");
            if (assignment.requiredPartySizeAtAcceptance > 0 && participants.Length < assignment.requiredPartySizeAtAcceptance) errors.Add($"Quest assignment '{assignment.assignmentId}' has {participants.Length} participant(s), below its accepted requirement of {assignment.requiredPartySizeAtAcceptance}.");
            if (string.IsNullOrWhiteSpace(assignment.worldId)) errors.Add($"Quest assignment '{assignment.assignmentId}' is missing world ID.");
            if (assignment.lifecycleState == QuestAssignmentLifecycleState.Unknown) errors.Add($"Quest assignment '{assignment.assignmentId}' has unknown lifecycle state.");
        }

        private static bool CanSee(QuestVisibility visibility, QuestVisibilityAccess access, string requesterPersonId, string ownerPersonId)
        {
            if (access == QuestVisibilityAccess.PrivilegedDiagnostic) return true;
            if (visibility == QuestVisibility.Hidden || visibility == QuestVisibility.Diagnostic || visibility == QuestVisibility.Development) return false;
            if (visibility == QuestVisibility.Secret) return access == QuestVisibilityAccess.Government || access == QuestVisibilityAccess.OrganizationMember;
            if (visibility == QuestVisibility.Restricted || visibility == QuestVisibility.OrganizationKnown || visibility == QuestVisibility.MemberKnown) return access == QuestVisibilityAccess.OrganizationMember || access == QuestVisibilityAccess.Government;
            if (visibility == QuestVisibility.GovernmentKnown) return access == QuestVisibilityAccess.Government;
            if (visibility == QuestVisibility.RecipientKnown) return access == QuestVisibilityAccess.Recipient && string.Equals(N(requesterPersonId), N(ownerPersonId), StringComparison.Ordinal);
            return true;
        }

        private static bool IsActiveAssignment(QuestAssignmentRecordData assignment)
        {
            return assignment != null && (assignment.lifecycleState == QuestAssignmentLifecycleState.Assigned || assignment.lifecycleState == QuestAssignmentLifecycleState.Active || assignment.lifecycleState == QuestAssignmentLifecycleState.Resumed || assignment.lifecycleState == QuestAssignmentLifecycleState.Suspended);
        }

        private static QuestOfferChannel ChannelFromQuest(QuestSnapshot quest)
        {
            return quest.Origin.sourceChannel switch
            {
                QuestSourceChannel.QuestBoard => QuestOfferChannel.QuestBoard,
                QuestSourceChannel.Government => QuestOfferChannel.GovernmentDesk,
                QuestSourceChannel.Organization => QuestOfferChannel.GuildCounter,
                QuestSourceChannel.Dialogue => QuestOfferChannel.DirectPerson,
                QuestSourceChannel.WorldEvent => QuestOfferChannel.NarrativeEvent,
                QuestSourceChannel.Discovery => QuestOfferChannel.TravelEncounter,
                QuestSourceChannel.System => QuestOfferChannel.SystemGenerated,
                _ => QuestOfferChannel.DirectInstitution
            };
        }

        private static string BuildOfferId(string questId, QuestRecipientReferenceData recipient, int index) => $"quest-offer.{N(questId)}.{recipient?.StableKey.Replace(':', '-').Replace('.', '-')}.{index:000}";
        private static string BuildAssignmentId(string questId, string personId, int index) => $"quest-assignment.{N(questId)}.{N(personId).Replace('.', '-')}.{index:000}";
        private static string N(string value) => QuestParticipationModelUtility.N(value);
    }
}
