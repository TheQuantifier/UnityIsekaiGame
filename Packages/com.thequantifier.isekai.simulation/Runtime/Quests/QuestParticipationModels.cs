using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Quests
{
    [Serializable]
    public sealed class QuestEligibilityRequirementData
    {
        public string requirementId;
        public QuestEligibilityRequirementKind kind;
        public string requiredId;
        public string secondaryId;
        public int minimumValue;
        public int maximumValue;
        public QuestRequirementComparison comparison = QuestRequirementComparison.Exists;
        public bool revealFailure = true;
        public bool negate;

        public QuestEligibilityRequirementData Clone()
        {
            return new QuestEligibilityRequirementData
            {
                requirementId = N(requirementId),
                kind = kind,
                requiredId = N(requiredId),
                secondaryId = N(secondaryId),
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                comparison = comparison,
                revealFailure = revealFailure,
                negate = negate
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class QuestEligibilityRequirementGroupData
    {
        public string groupId;
        public QuestEligibilityGroupPolicy policy = QuestEligibilityGroupPolicy.All;
        public int thresholdCount;
        public QuestEligibilityRequirementData[] requirements = Array.Empty<QuestEligibilityRequirementData>();
        public bool revealFailures = true;

        public QuestEligibilityRequirementGroupData Clone()
        {
            return new QuestEligibilityRequirementGroupData
            {
                groupId = N(groupId),
                policy = policy,
                thresholdCount = thresholdCount,
                requirements = (requirements ?? Array.Empty<QuestEligibilityRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                revealFailures = revealFailures
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class QuestEligibilityFactSet
    {
        private readonly HashSet<string> activePersons;
        private readonly HashSet<string> capabilities;
        private readonly Dictionary<string, int> skills;
        private readonly HashSet<string> traits;
        private readonly HashSet<string> possessedItems;
        private readonly HashSet<string> equippedItems;
        private readonly HashSet<string> professions;
        private readonly HashSet<string> qualifications;
        private readonly HashSet<string> credentials;
        private readonly HashSet<string> employments;
        private readonly HashSet<string> organizationMemberships;
        private readonly HashSet<string> organizationRanks;
        private readonly HashSet<string> offices;
        private readonly HashSet<string> authorityGrants;
        private readonly HashSet<string> factions;
        private readonly Dictionary<string, int> reputations;
        private readonly Dictionary<string, int> relationships;
        private readonly HashSet<string> citizenships;
        private readonly HashSet<string> residencies;
        private readonly HashSet<string> legalStatuses;
        private readonly HashSet<string> permits;
        private readonly HashSet<string> knownSubjects;
        private readonly HashSet<string> priorQuestStates;
        private readonly HashSet<string> historyFacts;
        private readonly HashSet<string> narrativeStates;

        public QuestEligibilityFactSet(
            IEnumerable<string> activePersons = null,
            IEnumerable<string> capabilities = null,
            IDictionary<string, int> skills = null,
            IEnumerable<string> traits = null,
            IEnumerable<string> possessedItems = null,
            IEnumerable<string> equippedItems = null,
            IEnumerable<string> professions = null,
            IEnumerable<string> qualifications = null,
            IEnumerable<string> credentials = null,
            IEnumerable<string> employments = null,
            IEnumerable<string> organizationMemberships = null,
            IEnumerable<string> organizationRanks = null,
            IEnumerable<string> offices = null,
            IEnumerable<string> authorityGrants = null,
            IEnumerable<string> factions = null,
            IDictionary<string, int> reputations = null,
            IDictionary<string, int> relationships = null,
            IEnumerable<string> citizenships = null,
            IEnumerable<string> residencies = null,
            IEnumerable<string> legalStatuses = null,
            IEnumerable<string> permits = null,
            IEnumerable<string> knownSubjects = null,
            IEnumerable<string> priorQuestStates = null,
            IEnumerable<string> historyFacts = null,
            IEnumerable<string> narrativeStates = null)
        {
            this.activePersons = Set(activePersons);
            this.capabilities = Set(capabilities);
            this.skills = Map(skills);
            this.traits = Set(traits);
            this.possessedItems = Set(possessedItems);
            this.equippedItems = Set(equippedItems);
            this.professions = Set(professions);
            this.qualifications = Set(qualifications);
            this.credentials = Set(credentials);
            this.employments = Set(employments);
            this.organizationMemberships = Set(organizationMemberships);
            this.organizationRanks = Set(organizationRanks);
            this.offices = Set(offices);
            this.authorityGrants = Set(authorityGrants);
            this.factions = Set(factions);
            this.reputations = Map(reputations);
            this.relationships = Map(relationships);
            this.citizenships = Set(citizenships);
            this.residencies = Set(residencies);
            this.legalStatuses = Set(legalStatuses);
            this.permits = Set(permits);
            this.knownSubjects = Set(knownSubjects);
            this.priorQuestStates = Set(priorQuestStates);
            this.historyFacts = Set(historyFacts);
            this.narrativeStates = Set(narrativeStates);
        }

        public static QuestEligibilityFactSet Empty { get; } = new QuestEligibilityFactSet();

        public bool Contains(QuestEligibilityRequirementKind kind, string id)
        {
            id = N(id);
            return kind switch
            {
                QuestEligibilityRequirementKind.PersonActive => activePersons.Contains(id),
                QuestEligibilityRequirementKind.Capability => capabilities.Contains(id),
                QuestEligibilityRequirementKind.Trait => traits.Contains(id),
                QuestEligibilityRequirementKind.ItemPossessed => possessedItems.Contains(id),
                QuestEligibilityRequirementKind.ItemEquipped => equippedItems.Contains(id),
                QuestEligibilityRequirementKind.Profession => professions.Contains(id),
                QuestEligibilityRequirementKind.Qualification => qualifications.Contains(id),
                QuestEligibilityRequirementKind.Credential => credentials.Contains(id),
                QuestEligibilityRequirementKind.Employment => employments.Contains(id),
                QuestEligibilityRequirementKind.OrganizationMembership => organizationMemberships.Contains(id),
                QuestEligibilityRequirementKind.OrganizationRank => organizationRanks.Contains(id),
                QuestEligibilityRequirementKind.Office => offices.Contains(id),
                QuestEligibilityRequirementKind.InstitutionalAuthority => authorityGrants.Contains(id),
                QuestEligibilityRequirementKind.FactionAffiliation => factions.Contains(id),
                QuestEligibilityRequirementKind.Citizenship => citizenships.Contains(id),
                QuestEligibilityRequirementKind.Residency => residencies.Contains(id),
                QuestEligibilityRequirementKind.LegalStatus => legalStatuses.Contains(id),
                QuestEligibilityRequirementKind.Permit => permits.Contains(id),
                QuestEligibilityRequirementKind.Knowledge => knownSubjects.Contains(id),
                QuestEligibilityRequirementKind.PriorQuestState => priorQuestStates.Contains(id),
                QuestEligibilityRequirementKind.WorldHistoryFact => historyFacts.Contains(id),
                QuestEligibilityRequirementKind.NarrativeState => narrativeStates.Contains(id),
                QuestEligibilityRequirementKind.Custom => capabilities.Contains(id) || credentials.Contains(id) || authorityGrants.Contains(id),
                _ => false
            };
        }

        public int Value(QuestEligibilityRequirementKind kind, string id)
        {
            id = N(id);
            return kind switch
            {
                QuestEligibilityRequirementKind.Skill => skills.TryGetValue(id, out int skill) ? skill : int.MinValue,
                QuestEligibilityRequirementKind.Reputation => reputations.TryGetValue(id, out int reputation) ? reputation : int.MinValue,
                QuestEligibilityRequirementKind.Relationship => relationships.TryGetValue(id, out int relationship) ? relationship : int.MinValue,
                _ => Contains(kind, id) ? 1 : int.MinValue
            };
        }

        private static HashSet<string> Set(IEnumerable<string> values)
        {
            return new HashSet<string>((values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
        }

        private static Dictionary<string, int> Map(IDictionary<string, int> values)
        {
            return (values ?? new Dictionary<string, int>()).Where(value => !string.IsNullOrWhiteSpace(value.Key)).ToDictionary(value => value.Key.Trim(), value => value.Value, StringComparer.Ordinal);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class QuestEligibilityContext
    {
        public string personId;
        public string partyId;
        public string[] partyMemberPersonIds = Array.Empty<string>();
        public string[] readyPartyMemberPersonIds = Array.Empty<string>();
        public string locationId;
        public string interactionPointId;
        public double worldTime;
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public bool privilegedDiagnostics;
        public QuestEligibilityFactSet facts = QuestEligibilityFactSet.Empty;

        public int PartySize
        {
            get
            {
                string[] members = CleanPartyMembers(partyMemberPersonIds);
                if (members.Length > 0)
                {
                    return members.Length;
                }

                return string.IsNullOrWhiteSpace(personId) ? 0 : 1;
            }
        }

        public int ReadyPartySize
        {
            get
            {
                string[] members = CleanPartyMembers(readyPartyMemberPersonIds);
                return !string.IsNullOrWhiteSpace(partyId) ? members.Length : PartySize;
            }
        }

        public QuestEligibilityContext Clone()
        {
            return new QuestEligibilityContext
            {
                personId = N(personId),
                partyId = N(partyId),
                partyMemberPersonIds = CleanPartyMembers(partyMemberPersonIds),
                readyPartyMemberPersonIds = CleanPartyMembers(readyPartyMemberPersonIds),
                locationId = N(locationId),
                interactionPointId = N(interactionPointId),
                worldTime = worldTime,
                access = access,
                privilegedDiagnostics = privilegedDiagnostics,
                facts = facts ?? QuestEligibilityFactSet.Empty
            };
        }

        private static string[] CleanPartyMembers(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class QuestOfferRecordData
    {
        public string offerId;
        public string questId;
        public string worldId;
        public QuestRecipientReferenceData recipient = new QuestRecipientReferenceData();
        public QuestIssuerReferenceData institutionalIssuer = new QuestIssuerReferenceData();
        public QuestIssuerReferenceData offeringProvider = new QuestIssuerReferenceData();
        public QuestOfferChannel channel = QuestOfferChannel.DirectInstitution;
        public string sourceInteractionPointId;
        public string sourceLocationId;
        public double createdWorldTime;
        public double expirationWorldTime = -1d;
        public QuestOfferLifecycleState lifecycleState = QuestOfferLifecycleState.Active;
        public QuestVisibility visibility = QuestVisibility.Public;
        public string authorityBasisId;
        public string eligibilityFingerprint;
        public string sourceEventId;
        public string provenanceId;
        public long revision = 1L;

        public QuestOfferRecordData Clone()
        {
            return new QuestOfferRecordData
            {
                offerId = N(offerId),
                questId = N(questId),
                worldId = N(worldId),
                recipient = recipient?.Clone() ?? new QuestRecipientReferenceData(),
                institutionalIssuer = institutionalIssuer?.Clone() ?? new QuestIssuerReferenceData(),
                offeringProvider = offeringProvider?.Clone() ?? new QuestIssuerReferenceData(),
                channel = channel,
                sourceInteractionPointId = N(sourceInteractionPointId),
                sourceLocationId = N(sourceLocationId),
                createdWorldTime = createdWorldTime,
                expirationWorldTime = expirationWorldTime,
                lifecycleState = lifecycleState,
                visibility = visibility,
                authorityBasisId = N(authorityBasisId),
                eligibilityFingerprint = N(eligibilityFingerprint),
                sourceEventId = N(sourceEventId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class QuestAssignmentRecordData
    {
        public string assignmentId;
        public string questId;
        public string offerId;
        public string worldId;
        public string assigneePersonId;
        public string undertakingPartyId;
        public string[] participantPersonIds = Array.Empty<string>();
        public int requiredPartySizeAtAcceptance = 1;
        public QuestIssuerReferenceData institutionalIssuer = new QuestIssuerReferenceData();
        public QuestIssuerReferenceData assignedBy = new QuestIssuerReferenceData();
        public QuestAssignmentLifecycleState lifecycleState = QuestAssignmentLifecycleState.Active;
        public QuestAssignmentCategory category = QuestAssignmentCategory.AcceptedOffer;
        public double assignedWorldTime;
        public double endedWorldTime = -1d;
        public QuestVisibility visibility = QuestVisibility.Public;
        public string consentRecordId;
        public string authorityBasisId;
        public string sourceEventId;
        public string provenanceId;
        public long revision = 1L;

        public QuestAssignmentRecordData Clone()
        {
            return new QuestAssignmentRecordData
            {
                assignmentId = N(assignmentId),
                questId = N(questId),
                offerId = N(offerId),
                worldId = N(worldId),
                assigneePersonId = N(assigneePersonId),
                undertakingPartyId = N(undertakingPartyId),
                participantPersonIds = CleanParticipants(participantPersonIds, assigneePersonId),
                requiredPartySizeAtAcceptance = Math.Max(1, requiredPartySizeAtAcceptance),
                institutionalIssuer = institutionalIssuer?.Clone() ?? new QuestIssuerReferenceData(),
                assignedBy = assignedBy?.Clone() ?? new QuestIssuerReferenceData(),
                lifecycleState = lifecycleState,
                category = category,
                assignedWorldTime = assignedWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                consentRecordId = N(consentRecordId),
                authorityBasisId = N(authorityBasisId),
                sourceEventId = N(sourceEventId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        internal static string[] CleanParticipants(IEnumerable<string> values, string fallbackAssignee = null)
        {
            IEnumerable<string> participants = values ?? Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(fallbackAssignee)) participants = participants.Concat(new[] { fallbackAssignee });
            return participants
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class QuestParticipationEventData
    {
        public string eventId;
        public string transactionId;
        public string questId;
        public string offerId;
        public string assignmentId;
        public string personId;
        public QuestParticipationEventKind eventKind;
        public double worldTime;
        public string sourceEventId;
        public string provenanceId;
        public long runtimeRevision;

        public QuestParticipationEventData Clone()
        {
            return new QuestParticipationEventData
            {
                eventId = eventId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                questId = questId ?? string.Empty,
                offerId = offerId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                eventKind = eventKind,
                worldTime = worldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestParticipationTransactionData
    {
        public string transactionId;
        public string operation;
        public string questId;
        public string offerId;
        public string assignmentId;
        public long runtimeRevision;

        public QuestParticipationTransactionData Clone()
        {
            return new QuestParticipationTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                questId = questId ?? string.Empty,
                offerId = offerId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestParticipationRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<QuestOfferRecordData> offers = new List<QuestOfferRecordData>();
        public List<QuestAssignmentRecordData> assignments = new List<QuestAssignmentRecordData>();
        public List<QuestParticipationEventData> events = new List<QuestParticipationEventData>();
        public List<QuestParticipationTransactionData> transactions = new List<QuestParticipationTransactionData>();

        public QuestParticipationRuntimeSaveData Clone()
        {
            return new QuestParticipationRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                offers = (offers ?? new List<QuestOfferRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                assignments = (assignments ?? new List<QuestAssignmentRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                events = (events ?? new List<QuestParticipationEventData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<QuestParticipationTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    public sealed class QuestAvailabilityResult
    {
        public QuestAvailabilityResult(string questId, QuestAvailabilityState state, bool available, int assignmentCapacity, int activeAssignments, IEnumerable<string> reasons, long questRevision, long participationRevision)
        {
            QuestId = questId ?? string.Empty;
            State = state;
            Available = available;
            AssignmentCapacity = assignmentCapacity;
            ActiveAssignmentCount = activeAssignments;
            Reasons = (reasons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            QuestRevision = questRevision;
            ParticipationRevision = participationRevision;
        }

        public string QuestId { get; }
        public QuestAvailabilityState State { get; }
        public bool Available { get; }
        public int AssignmentCapacity { get; }
        public int ActiveAssignmentCount { get; }
        public IReadOnlyList<string> Reasons { get; }
        public long QuestRevision { get; }
        public long ParticipationRevision { get; }
        public string Fingerprint => $"{QuestId}:{State}:{AssignmentCapacity}:{ActiveAssignmentCount}:{QuestRevision}:{ParticipationRevision}:{string.Join(",", Reasons)}";
    }

    public sealed class QuestEligibilityResult
    {
        public QuestEligibilityResult(string questId, string personId, QuestAvailabilityResult availability, bool eligible, IEnumerable<string> visibleFailureReasons, int hiddenFailureCount, long sourceRevision)
        {
            QuestId = questId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            Availability = availability;
            Eligible = eligible;
            VisibleFailureReasons = (visibleFailureReasons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            HiddenFailureCount = hiddenFailureCount;
            SourceRevision = sourceRevision;
        }

        public string QuestId { get; }
        public string PersonId { get; }
        public QuestAvailabilityResult Availability { get; }
        public bool Eligible { get; }
        public IReadOnlyList<string> VisibleFailureReasons { get; }
        public int HiddenFailureCount { get; }
        public long SourceRevision { get; }
        public string Fingerprint => $"{QuestId}:{PersonId}:{Eligible}:{Availability?.Fingerprint}:{HiddenFailureCount}:{string.Join(",", VisibleFailureReasons)}";
    }

    public sealed class QuestOfferSnapshot
    {
        private readonly QuestOfferRecordData data;

        public QuestOfferSnapshot(QuestOfferRecordData record)
        {
            data = record?.Clone() ?? new QuestOfferRecordData();
        }

        public string OfferId => data.offerId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public QuestRecipientReferenceData Recipient => data.recipient?.Clone() ?? new QuestRecipientReferenceData();
        public QuestIssuerReferenceData InstitutionalIssuer => data.institutionalIssuer?.Clone() ?? new QuestIssuerReferenceData();
        public QuestIssuerReferenceData OfferingProvider => data.offeringProvider?.Clone() ?? new QuestIssuerReferenceData();
        public QuestOfferChannel Channel => data.channel;
        public string SourceInteractionPointId => data.sourceInteractionPointId ?? string.Empty;
        public string SourceLocationId => data.sourceLocationId ?? string.Empty;
        public double CreatedWorldTime => data.createdWorldTime;
        public double ExpirationWorldTime => data.expirationWorldTime;
        public QuestOfferLifecycleState LifecycleState => data.lifecycleState;
        public QuestVisibility Visibility => data.visibility;
        public string AuthorityBasisId => data.authorityBasisId ?? string.Empty;
        public string EligibilityFingerprint => data.eligibilityFingerprint ?? string.Empty;
        public long Revision => data.revision;
        public QuestOfferRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestAssignmentSnapshot
    {
        private readonly QuestAssignmentRecordData data;

        public QuestAssignmentSnapshot(QuestAssignmentRecordData record)
        {
            data = record?.Clone() ?? new QuestAssignmentRecordData();
        }

        public string AssignmentId => data.assignmentId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string OfferId => data.offerId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public string AssigneePersonId => data.assigneePersonId ?? string.Empty;
        public string UndertakingPartyId => data.undertakingPartyId ?? string.Empty;
        public IReadOnlyList<string> ParticipantPersonIds => QuestAssignmentRecordData.CleanParticipants(data.participantPersonIds, data.assigneePersonId);
        public int RequiredPartySizeAtAcceptance => Math.Max(1, data.requiredPartySizeAtAcceptance);
        public bool IsPartyAssignment => !string.IsNullOrWhiteSpace(UndertakingPartyId) || ParticipantPersonIds.Count > 1;
        public QuestIssuerReferenceData InstitutionalIssuer => data.institutionalIssuer?.Clone() ?? new QuestIssuerReferenceData();
        public QuestIssuerReferenceData AssignedBy => data.assignedBy?.Clone() ?? new QuestIssuerReferenceData();
        public QuestAssignmentLifecycleState LifecycleState => data.lifecycleState;
        public QuestAssignmentCategory Category => data.category;
        public double AssignedWorldTime => data.assignedWorldTime;
        public double EndedWorldTime => data.endedWorldTime;
        public QuestVisibility Visibility => data.visibility;
        public string ConsentRecordId => data.consentRecordId ?? string.Empty;
        public string AuthorityBasisId => data.authorityBasisId ?? string.Empty;
        public long Revision => data.revision;
        public QuestAssignmentRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestOfferRequest
    {
        public string transactionId;
        public string offerId;
        public string questId;
        public QuestRecipientReferenceData recipient;
        public QuestIssuerReferenceData institutionalIssuer;
        public QuestIssuerReferenceData offeringProvider;
        public QuestOfferChannel channel = QuestOfferChannel.DirectInstitution;
        public string sourceInteractionPointId;
        public string sourceLocationId;
        public double worldTime;
        public double expirationWorldTime = -1d;
        public QuestVisibility? visibility;
        public string authorityBasisId;
        public QuestEligibilityContext eligibilityContext;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestAcceptOfferRequest
    {
        public string transactionId;
        public string offerId;
        public string assignmentId;
        public string personId;
        public bool explicitConsent;
        public string consentRecordId;
        public string authorityBasisId;
        public QuestEligibilityContext eligibilityContext;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestDirectAssignmentRequest
    {
        public string transactionId;
        public string assignmentId;
        public string questId;
        public string assigneePersonId;
        public QuestIssuerReferenceData institutionalIssuer;
        public QuestIssuerReferenceData assignedBy;
        public bool explicitConsent;
        public string consentRecordId;
        public string authorityBasisId;
        public QuestEligibilityContext eligibilityContext;
        public double worldTime;
        public QuestVisibility? visibility;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestOfferLifecycleRequest
    {
        public string transactionId;
        public string offerId;
        public string actingPersonId;
        public string authorityBasisId;
        public QuestOfferLifecycleState targetState;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestAssignmentLifecycleRequest
    {
        public string transactionId;
        public string assignmentId;
        public string actingPersonId;
        public string authorityBasisId;
        public QuestAssignmentLifecycleState targetState;
        public bool explicitConsent;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestOfferQuery
    {
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public string questId;
        public string offerId;
        public string recipientPersonId;
        public string issuerId;
        public string providerPersonId;
        public string interactionPointId;
        public QuestOfferLifecycleState? lifecycleState;
        public bool includeHistorical;
        public string worldId;
    }

    public sealed class QuestAssignmentQuery
    {
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public string questId;
        public string assignmentId;
        public string assigneePersonId;
        public string participantPersonId;
        public string issuerId;
        public QuestAssignmentLifecycleState? lifecycleState;
        public bool includeHistorical;
        public string worldId;
    }

    public sealed class QuestParticipationSummary
    {
        public QuestParticipationSummary(string questId, int visibleOffers, int visibleAssignments, bool countsRedacted)
        {
            QuestId = questId ?? string.Empty;
            VisibleOffers = visibleOffers;
            VisibleAssignments = visibleAssignments;
            CountsRedacted = countsRedacted;
        }

        public string QuestId { get; }
        public int VisibleOffers { get; }
        public int VisibleAssignments { get; }
        public bool CountsRedacted { get; }
    }

    public sealed class QuestParticipationOperationResult
    {
        private QuestParticipationOperationResult(QuestParticipationOperationStatus status, string message, QuestOfferSnapshot offer, QuestAssignmentSnapshot assignment, QuestAvailabilityResult availability, QuestEligibilityResult eligibility, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Offer = offer;
            Assignment = assignment;
            Availability = availability;
            Eligibility = eligibility;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public QuestParticipationOperationStatus Status { get; }
        public string Message { get; }
        public QuestOfferSnapshot Offer { get; }
        public QuestAssignmentSnapshot Assignment { get; }
        public QuestAvailabilityResult Availability { get; }
        public QuestEligibilityResult Eligibility { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == QuestParticipationOperationStatus.Succeeded || Status == QuestParticipationOperationStatus.Preview || Status == QuestParticipationOperationStatus.Duplicate;

        public static QuestParticipationOperationResult Success(string message, long before, long after, QuestOfferSnapshot offer = null, QuestAssignmentSnapshot assignment = null, QuestAvailabilityResult availability = null, QuestEligibilityResult eligibility = null, bool preview = false, bool duplicate = false)
        {
            return new QuestParticipationOperationResult(preview ? QuestParticipationOperationStatus.Preview : duplicate ? QuestParticipationOperationStatus.Duplicate : QuestParticipationOperationStatus.Succeeded, message, offer, assignment, availability, eligibility, preview, duplicate, before, after);
        }

        public static QuestParticipationOperationResult Failure(QuestParticipationOperationStatus status, string message, long revision, QuestAvailabilityResult availability = null, QuestEligibilityResult eligibility = null)
        {
            return new QuestParticipationOperationResult(status, message, null, null, availability, eligibility, false, false, revision, revision);
        }
    }

    public sealed class QuestParticipationValidationReport
    {
        public QuestParticipationValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Quest participation validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";
    }

    public static class QuestParticipationModelUtility
    {
        public static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        public static string[] Clean(IEnumerable<string> values) => QuestRuntimeModelUtility.Clean(values);
        public static bool WorldMatches(string actual, string expected) => string.Equals(N(actual), N(string.IsNullOrWhiteSpace(expected) ? PersistenceService.LocalWorldId : expected), StringComparison.Ordinal);
    }
}
