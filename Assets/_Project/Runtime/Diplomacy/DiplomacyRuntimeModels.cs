using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Diplomacy
{
    public static class DiplomacyModelUtility
    {
        public static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool IsSecret(DiplomaticVisibility visibility)
        {
            return visibility == DiplomaticVisibility.Confidential
                || visibility == DiplomaticVisibility.Secret
                || visibility == DiplomaticVisibility.Hidden
                || visibility == DiplomaticVisibility.DevelopmentOnly;
        }
    }

    [Serializable]
    public sealed class DiplomaticActorReferenceData : IEquatable<DiplomaticActorReferenceData>
    {
        public DiplomaticActorKind actorKind = DiplomaticActorKind.Organization;
        public string actorId;
        public string worldId;

        public string StableKey => $"{actorKind}:{DiplomacyModelUtility.Normalize(worldId)}:{DiplomacyModelUtility.Normalize(actorId)}";

        public DiplomaticActorReferenceData Clone()
        {
            return new DiplomaticActorReferenceData
            {
                actorKind = actorKind,
                actorId = actorId ?? string.Empty,
                worldId = worldId ?? string.Empty
            };
        }

        public bool Equals(DiplomaticActorReferenceData other)
        {
            return other != null
                && actorKind == other.actorKind
                && string.Equals(DiplomacyModelUtility.Normalize(actorId), DiplomacyModelUtility.Normalize(other.actorId), StringComparison.Ordinal)
                && string.Equals(DiplomacyModelUtility.Normalize(worldId), DiplomacyModelUtility.Normalize(other.worldId), StringComparison.Ordinal);
        }

        public static DiplomaticActorReferenceData Organization(string organizationId, string worldId = "")
        {
            return new DiplomaticActorReferenceData { actorKind = DiplomaticActorKind.Organization, actorId = DiplomacyModelUtility.Normalize(organizationId), worldId = DiplomacyModelUtility.Normalize(worldId) };
        }

        public static DiplomaticActorReferenceData Faction(string factionId, string worldId = "")
        {
            return new DiplomaticActorReferenceData { actorKind = DiplomaticActorKind.Faction, actorId = DiplomacyModelUtility.Normalize(factionId), worldId = DiplomacyModelUtility.Normalize(worldId) };
        }
    }

    [Serializable]
    public sealed class DiplomaticRelationRecordData
    {
        public string relationId;
        public string relationDefinitionId;
        public DiplomaticActorReferenceData sourceActor = new DiplomaticActorReferenceData();
        public DiplomaticActorReferenceData targetActor = new DiplomaticActorReferenceData();
        public DiplomaticRelationCategory category = DiplomaticRelationCategory.Neutral;
        public DiplomaticLifecycleState lifecycleState = DiplomaticLifecycleState.Active;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string sourceAgreementId;
        public string sourceWarId;
        public string sourceDecisionId;
        public string sourceAuthorityGrantId;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public string publicSummary;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public bool IsActive => lifecycleState == DiplomaticLifecycleState.Active || lifecycleState == DiplomaticLifecycleState.PendingRecognition;

        public DiplomaticRelationRecordData Clone()
        {
            return new DiplomaticRelationRecordData
            {
                relationId = relationId ?? string.Empty,
                relationDefinitionId = relationDefinitionId ?? string.Empty,
                sourceActor = sourceActor?.Clone() ?? new DiplomaticActorReferenceData(),
                targetActor = targetActor?.Clone() ?? new DiplomaticActorReferenceData(),
                category = category,
                lifecycleState = lifecycleState,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                sourceAgreementId = sourceAgreementId ?? string.Empty,
                sourceWarId = sourceWarId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                sourceAuthorityGrantId = sourceAuthorityGrantId ?? string.Empty,
                visibility = visibility,
                publicSummary = publicSummary ?? string.Empty,
                tags = DiplomacyModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticAgreementPartyRecordData
    {
        public string partyId;
        public DiplomaticActorReferenceData actor = new DiplomaticActorReferenceData();
        public DiplomaticPartyRole role = DiplomaticPartyRole.Principal;
        public string representativePersonId;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public double joinedWorldTime;
        public double withdrawnWorldTime = -1d;
        public bool active = true;

        public DiplomaticAgreementPartyRecordData Clone()
        {
            return new DiplomaticAgreementPartyRecordData
            {
                partyId = partyId ?? string.Empty,
                actor = actor?.Clone() ?? new DiplomaticActorReferenceData(),
                role = role,
                representativePersonId = representativePersonId ?? string.Empty,
                sourceAuthorityGrantId = sourceAuthorityGrantId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                joinedWorldTime = joinedWorldTime,
                withdrawnWorldTime = withdrawnWorldTime,
                active = active
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticClauseParameterData
    {
        public string parameterId;
        public DiplomaticClauseParameterType valueType = DiplomaticClauseParameterType.Text;
        public string stringValue;
        public int intValue;
        public double decimalValue;
        public bool boolValue;
        public DiplomaticActorReferenceData actorValue;

        public DiplomaticClauseParameterData Clone()
        {
            return new DiplomaticClauseParameterData
            {
                parameterId = parameterId ?? string.Empty,
                valueType = valueType,
                stringValue = stringValue ?? string.Empty,
                intValue = intValue,
                decimalValue = decimalValue,
                boolValue = boolValue,
                actorValue = actorValue?.Clone()
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticClauseRecordData
    {
        public string clauseId;
        public string agreementId;
        public string clauseDefinitionId;
        public DiplomaticClauseCategory category = DiplomaticClauseCategory.Custom;
        public DiplomaticClauseLifecycleState lifecycleState = DiplomaticClauseLifecycleState.Active;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public DiplomaticClauseParameterData[] parameters = Array.Empty<DiplomaticClauseParameterData>();
        public string referencedContractId;
        public string referencedResourceId;
        public string sourceDecisionId;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public long revision = 1L;

        public DiplomaticClauseRecordData Clone()
        {
            return new DiplomaticClauseRecordData
            {
                clauseId = clauseId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                clauseDefinitionId = clauseDefinitionId ?? string.Empty,
                category = category,
                lifecycleState = lifecycleState,
                visibility = visibility,
                parameters = parameters == null ? Array.Empty<DiplomaticClauseParameterData>() : parameters.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                referencedContractId = referencedContractId ?? string.Empty,
                referencedResourceId = referencedResourceId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                expirationWorldTime = expirationWorldTime,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticSignatureRecordData
    {
        public string signatureId;
        public string agreementId;
        public string partyId;
        public string signerPersonId;
        public string authorityGrantId;
        public DiplomaticSignatureStatus status = DiplomaticSignatureStatus.Signed;
        public double signedWorldTime;
        public long revision = 1L;

        public DiplomaticSignatureRecordData Clone()
        {
            return new DiplomaticSignatureRecordData
            {
                signatureId = signatureId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                partyId = partyId ?? string.Empty,
                signerPersonId = signerPersonId ?? string.Empty,
                authorityGrantId = authorityGrantId ?? string.Empty,
                status = status,
                signedWorldTime = signedWorldTime,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticRatificationRecordData
    {
        public string ratificationId;
        public string agreementId;
        public string partyId;
        public string sourceDecisionId;
        public DiplomaticRatificationStatus status = DiplomaticRatificationStatus.Ratified;
        public double ratifiedWorldTime;

        public DiplomaticRatificationRecordData Clone()
        {
            return new DiplomaticRatificationRecordData
            {
                ratificationId = ratificationId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                partyId = partyId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                status = status,
                ratifiedWorldTime = ratifiedWorldTime
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticBreachRecordData
    {
        public string breachId;
        public string agreementId;
        public string clauseId;
        public DiplomaticActorReferenceData allegedActor = new DiplomaticActorReferenceData();
        public DiplomaticBreachState state = DiplomaticBreachState.Alleged;
        public double reportedWorldTime;
        public double resolvedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string notes;

        public DiplomaticBreachRecordData Clone()
        {
            return new DiplomaticBreachRecordData
            {
                breachId = breachId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                clauseId = clauseId ?? string.Empty,
                allegedActor = allegedActor?.Clone() ?? new DiplomaticActorReferenceData(),
                state = state,
                reportedWorldTime = reportedWorldTime,
                resolvedWorldTime = resolvedWorldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                notes = notes ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticAgreementRecordData
    {
        public string agreementId;
        public string agreementDefinitionId;
        public string title;
        public DiplomaticAgreementCategory category = DiplomaticAgreementCategory.Cooperation;
        public DiplomaticAgreementLifecycleState lifecycleState = DiplomaticAgreementLifecycleState.Draft;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public DiplomaticAgreementPartyRecordData[] parties = Array.Empty<DiplomaticAgreementPartyRecordData>();
        public string[] clauseIds = Array.Empty<string>();
        public string[] amendmentIds = Array.Empty<string>();
        public string sourceProposalId;
        public string sourceResolutionId;
        public string sourceContractId;
        public double draftedWorldTime;
        public double effectiveWorldTime = -1d;
        public double expirationWorldTime = -1d;
        public long revision = 1L;

        public bool IsActive => lifecycleState == DiplomaticAgreementLifecycleState.Active || lifecycleState == DiplomaticAgreementLifecycleState.Ratified || lifecycleState == DiplomaticAgreementLifecycleState.Signed;

        public DiplomaticAgreementRecordData Clone()
        {
            return new DiplomaticAgreementRecordData
            {
                agreementId = agreementId ?? string.Empty,
                agreementDefinitionId = agreementDefinitionId ?? string.Empty,
                title = title ?? string.Empty,
                category = category,
                lifecycleState = lifecycleState,
                visibility = visibility,
                parties = parties == null ? Array.Empty<DiplomaticAgreementPartyRecordData>() : parties.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                clauseIds = DiplomacyModelUtility.Clean(clauseIds),
                amendmentIds = DiplomacyModelUtility.Clean(amendmentIds),
                sourceProposalId = sourceProposalId ?? string.Empty,
                sourceResolutionId = sourceResolutionId ?? string.Empty,
                sourceContractId = sourceContractId ?? string.Empty,
                draftedWorldTime = draftedWorldTime,
                effectiveWorldTime = effectiveWorldTime,
                expirationWorldTime = expirationWorldTime,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticWarSideRecordData
    {
        public string sideId;
        public string warId;
        public string displayName;
        public DiplomaticActorReferenceData[] principalActors = Array.Empty<DiplomaticActorReferenceData>();

        public DiplomaticWarSideRecordData Clone()
        {
            return new DiplomaticWarSideRecordData
            {
                sideId = sideId ?? string.Empty,
                warId = warId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                principalActors = principalActors == null ? Array.Empty<DiplomaticActorReferenceData>() : principalActors.Select(item => item?.Clone()).Where(item => item != null).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticWarParticipationRecordData
    {
        public string participationId;
        public string warId;
        public string sideId;
        public DiplomaticActorReferenceData actor = new DiplomaticActorReferenceData();
        public DiplomaticWarParticipantStatus status = DiplomaticWarParticipantStatus.Belligerent;
        public string sourceAgreementId;
        public string sourceDecisionId;
        public double joinedWorldTime;
        public double leftWorldTime = -1d;

        public bool Active => status != DiplomaticWarParticipantStatus.Withdrawn && leftWorldTime < 0d;

        public DiplomaticWarParticipationRecordData Clone()
        {
            return new DiplomaticWarParticipationRecordData
            {
                participationId = participationId ?? string.Empty,
                warId = warId ?? string.Empty,
                sideId = sideId ?? string.Empty,
                actor = actor?.Clone() ?? new DiplomaticActorReferenceData(),
                status = status,
                sourceAgreementId = sourceAgreementId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                joinedWorldTime = joinedWorldTime,
                leftWorldTime = leftWorldTime
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticWarRecordData
    {
        public string warId;
        public string warDefinitionId;
        public string title;
        public DiplomaticWarCategory category = DiplomaticWarCategory.FormalWar;
        public DiplomaticWarLifecycleState lifecycleState = DiplomaticWarLifecycleState.Declared;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public string[] sideIds = Array.Empty<string>();
        public string[] participationIds = Array.Empty<string>();
        public string declarationRecordId;
        public string ceasefireAgreementId;
        public string peaceAgreementId;
        public double declaredWorldTime;
        public double endedWorldTime = -1d;
        public long revision = 1L;

        public DiplomaticWarRecordData Clone()
        {
            return new DiplomaticWarRecordData
            {
                warId = warId ?? string.Empty,
                warDefinitionId = warDefinitionId ?? string.Empty,
                title = title ?? string.Empty,
                category = category,
                lifecycleState = lifecycleState,
                visibility = visibility,
                sideIds = DiplomacyModelUtility.Clean(sideIds),
                participationIds = DiplomacyModelUtility.Clean(participationIds),
                declarationRecordId = declarationRecordId ?? string.Empty,
                ceasefireAgreementId = ceasefireAgreementId ?? string.Empty,
                peaceAgreementId = peaceAgreementId ?? string.Empty,
                declaredWorldTime = declaredWorldTime,
                endedWorldTime = endedWorldTime,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticIncidentRecordData
    {
        public string incidentId;
        public string warId;
        public string relationId;
        public DiplomaticIncidentCategory category = DiplomaticIncidentCategory.Custom;
        public DiplomaticActorReferenceData sourceActor = new DiplomaticActorReferenceData();
        public DiplomaticActorReferenceData targetActor = new DiplomaticActorReferenceData();
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string publicSummary;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;

        public DiplomaticIncidentRecordData Clone()
        {
            return new DiplomaticIncidentRecordData
            {
                incidentId = incidentId ?? string.Empty,
                warId = warId ?? string.Empty,
                relationId = relationId ?? string.Empty,
                category = category,
                sourceActor = sourceActor?.Clone() ?? new DiplomaticActorReferenceData(),
                targetActor = targetActor?.Clone() ?? new DiplomaticActorReferenceData(),
                worldTime = worldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                publicSummary = publicSummary ?? string.Empty,
                visibility = visibility
            };
        }
    }

    [Serializable]
    public sealed class DiplomaticTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public long revision;

        public DiplomaticTransactionRecordData Clone()
        {
            return new DiplomaticTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class DiplomacyRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<DiplomaticRelationRecordData> relations = new List<DiplomaticRelationRecordData>();
        public List<DiplomaticAgreementRecordData> agreements = new List<DiplomaticAgreementRecordData>();
        public List<DiplomaticClauseRecordData> clauses = new List<DiplomaticClauseRecordData>();
        public List<DiplomaticSignatureRecordData> signatures = new List<DiplomaticSignatureRecordData>();
        public List<DiplomaticRatificationRecordData> ratifications = new List<DiplomaticRatificationRecordData>();
        public List<DiplomaticBreachRecordData> breaches = new List<DiplomaticBreachRecordData>();
        public List<DiplomaticWarRecordData> wars = new List<DiplomaticWarRecordData>();
        public List<DiplomaticWarSideRecordData> warSides = new List<DiplomaticWarSideRecordData>();
        public List<DiplomaticWarParticipationRecordData> warParticipations = new List<DiplomaticWarParticipationRecordData>();
        public List<DiplomaticIncidentRecordData> incidents = new List<DiplomaticIncidentRecordData>();
        public List<DiplomaticTransactionRecordData> transactions = new List<DiplomaticTransactionRecordData>();

        public DiplomacyRuntimeSaveData Clone()
        {
            return new DiplomacyRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                relations = CloneList(relations),
                agreements = CloneList(agreements),
                clauses = CloneList(clauses),
                signatures = CloneList(signatures),
                ratifications = CloneList(ratifications),
                breaches = CloneList(breaches),
                wars = CloneList(wars),
                warSides = CloneList(warSides),
                warParticipations = CloneList(warParticipations),
                incidents = CloneList(incidents),
                transactions = CloneList(transactions)
            };
        }

        private static List<T> CloneList<T>(IEnumerable<T> source) where T : class
        {
            List<T> result = new List<T>();
            foreach (T item in source ?? Array.Empty<T>())
            {
                object clone = item switch
                {
                    DiplomaticRelationRecordData value => value.Clone(),
                    DiplomaticAgreementRecordData value => value.Clone(),
                    DiplomaticClauseRecordData value => value.Clone(),
                    DiplomaticSignatureRecordData value => value.Clone(),
                    DiplomaticRatificationRecordData value => value.Clone(),
                    DiplomaticBreachRecordData value => value.Clone(),
                    DiplomaticWarRecordData value => value.Clone(),
                    DiplomaticWarSideRecordData value => value.Clone(),
                    DiplomaticWarParticipationRecordData value => value.Clone(),
                    DiplomaticIncidentRecordData value => value.Clone(),
                    DiplomaticTransactionRecordData value => value.Clone(),
                    _ => null
                };
                if (clone is T typed) result.Add(typed);
            }
            return result;
        }
    }

    public sealed class DiplomaticRelationRequest
    {
        public string relationId;
        public string relationDefinitionId;
        public DiplomaticActorReferenceData sourceActor;
        public DiplomaticActorReferenceData targetActor;
        public DiplomaticRelationCategory category = DiplomaticRelationCategory.Unknown;
        public DiplomaticLifecycleState lifecycleState = DiplomaticLifecycleState.Active;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public double worldTime;
        public string sourceAgreementId;
        public string sourceWarId;
        public string sourceDecisionId;
        public string sourceAuthorityGrantId;
        public string publicSummary;
        public string transactionId;
        public bool preview;
    }

    public sealed class DiplomaticAgreementRequest
    {
        public string agreementId;
        public string agreementDefinitionId;
        public string title;
        public IEnumerable<DiplomaticAgreementPartyRecordData> parties;
        public IEnumerable<DiplomaticClauseRecordData> clauses;
        public DiplomaticAgreementLifecycleState initialState = DiplomaticAgreementLifecycleState.Draft;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public double worldTime;
        public string sourceProposalId;
        public string sourceResolutionId;
        public string sourceContractId;
        public string transactionId;
        public bool preview;
    }

    public sealed class DiplomaticSignatureRequest
    {
        public string signatureId;
        public string agreementId;
        public string partyId;
        public string signerPersonId;
        public string authorityGrantId;
        public double worldTime;
        public string transactionId;
        public bool preview;
    }

    public sealed class DiplomaticBreachRequest
    {
        public string breachId;
        public string agreementId;
        public string clauseId;
        public DiplomaticActorReferenceData allegedActor;
        public DiplomaticBreachState state = DiplomaticBreachState.Alleged;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string notes;
        public string transactionId;
        public bool preview;
    }

    public sealed class DiplomaticWarDeclarationRequest
    {
        public string warId;
        public string warDefinitionId;
        public string title;
        public DiplomaticActorReferenceData[] sideA = Array.Empty<DiplomaticActorReferenceData>();
        public DiplomaticActorReferenceData[] sideB = Array.Empty<DiplomaticActorReferenceData>();
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public double worldTime;
        public string declarationRecordId;
        public string sourceDecisionId;
        public string transactionId;
        public bool preview;
    }

    public sealed class DiplomaticIncidentRequest
    {
        public string incidentId;
        public string warId;
        public string relationId;
        public DiplomaticIncidentCategory category = DiplomaticIncidentCategory.Custom;
        public DiplomaticActorReferenceData sourceActor;
        public DiplomaticActorReferenceData targetActor;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string publicSummary;
        public DiplomaticVisibility visibility = DiplomaticVisibility.Public;
        public string transactionId;
        public bool preview;
    }

    public sealed class DiplomacyOperationResult
    {
        private DiplomacyOperationResult(bool succeeded, DiplomaticOperationCode code, string message, long beforeRevision, long afterRevision, bool preview, bool duplicate, string subjectId, DiplomaticRelationRecordData relation, DiplomaticAgreementRecordData agreement, DiplomaticClauseRecordData clause, DiplomaticWarRecordData war, DiplomaticIncidentRecordData incident)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
            SubjectId = subjectId ?? string.Empty;
            Relation = relation?.Clone();
            Agreement = agreement?.Clone();
            Clause = clause?.Clone();
            War = war?.Clone();
            Incident = incident?.Clone();
        }

        public bool Succeeded { get; }
        public DiplomaticOperationCode Code { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string SubjectId { get; }
        public DiplomaticRelationRecordData Relation { get; }
        public DiplomaticAgreementRecordData Agreement { get; }
        public DiplomaticClauseRecordData Clause { get; }
        public DiplomaticWarRecordData War { get; }
        public DiplomaticIncidentRecordData Incident { get; }

        public static DiplomacyOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, string subjectId = "", DiplomaticRelationRecordData relation = null, DiplomaticAgreementRecordData agreement = null, DiplomaticClauseRecordData clause = null, DiplomaticWarRecordData war = null, DiplomaticIncidentRecordData incident = null)
        {
            return new DiplomacyOperationResult(true, preview ? DiplomaticOperationCode.Preview : duplicate ? DiplomaticOperationCode.Duplicate : DiplomaticOperationCode.Succeeded, message, before, after, preview, duplicate, subjectId, relation, agreement, clause, war, incident);
        }

        public static DiplomacyOperationResult Failure(DiplomaticOperationCode code, string message, long before)
        {
            return new DiplomacyOperationResult(false, code, message, before, before, false, false, string.Empty, null, null, null, null, null);
        }
    }

    public sealed class DiplomaticProjection
    {
        public DiplomaticProjection(DiplomaticProjectionAccess access, string subjectId, DiplomaticVisibility visibility, object snapshot, string message)
        {
            Access = access;
            SubjectId = subjectId ?? string.Empty;
            Visibility = visibility;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public DiplomaticProjectionAccess Access { get; }
        public string SubjectId { get; }
        public DiplomaticVisibility Visibility { get; }
        public object Snapshot { get; }
        public string Message { get; }
        public bool Succeeded => Access == DiplomaticProjectionAccess.Full || Access == DiplomaticProjectionAccess.Privileged || Access == DiplomaticProjectionAccess.Redacted;
    }
}
