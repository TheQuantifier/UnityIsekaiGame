using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Factions
{
    public static class FactionModelUtility
    {
        public static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        public static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        public static T[] CleanEnums<T>(IEnumerable<T> values) where T : struct, Enum => (values ?? Array.Empty<T>()).Where(value => Enum.IsDefined(typeof(T), value)).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        public static IReadOnlyList<T> Ordered<T>(IEnumerable<T> values, Func<T, string> key) => (values ?? Array.Empty<T>()).OrderBy(key, StringComparer.Ordinal).ToArray();
        public static bool IsSecret(FactionVisibility visibility) => visibility == FactionVisibility.Hidden || visibility == FactionVisibility.Secret;
    }

    [Serializable]
    public sealed class FactionHostContextData
    {
        public FactionHostContextKind contextKind = FactionHostContextKind.Independent;
        public string primaryOrganizationId;
        public string branchOrganizationId;
        public string[] organizationIds = Array.Empty<string>();
        public string placeOrRegionId;
        public string populationAudienceId;
        public string provenanceId;

        public string StableKey
        {
            get
            {
                string[] organizations = FactionModelUtility.Clean(organizationIds);
                return $"{contextKind}|{primaryOrganizationId ?? string.Empty}|{branchOrganizationId ?? string.Empty}|{string.Join(",", organizations)}|{placeOrRegionId ?? string.Empty}|{populationAudienceId ?? string.Empty}";
            }
        }

        public FactionHostContextData Clone()
        {
            return new FactionHostContextData
            {
                contextKind = contextKind,
                primaryOrganizationId = primaryOrganizationId ?? string.Empty,
                branchOrganizationId = branchOrganizationId ?? string.Empty,
                organizationIds = FactionModelUtility.Clean(organizationIds),
                placeOrRegionId = placeOrRegionId ?? string.Empty,
                populationAudienceId = populationAudienceId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }

        public static FactionHostContextData ForOrganization(string organizationId) => new FactionHostContextData
        {
            contextKind = FactionHostContextKind.SingleOrganization,
            primaryOrganizationId = FactionModelUtility.Normalize(organizationId),
            organizationIds = string.IsNullOrWhiteSpace(organizationId) ? Array.Empty<string>() : new[] { organizationId.Trim() }
        };

        public static FactionHostContextData Independent() => new FactionHostContextData { contextKind = FactionHostContextKind.Independent };
    }

    [Serializable]
    public sealed class FactionNameRecordData
    {
        public string nameRecordId;
        public string factionId;
        public string value;
        public FactionNameCategory category = FactionNameCategory.Official;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActive => effectiveEndWorldTime < 0d;

        public FactionNameRecordData Clone()
        {
            return new FactionNameRecordData
            {
                nameRecordId = nameRecordId ?? string.Empty,
                factionId = factionId ?? string.Empty,
                value = value ?? string.Empty,
                category = category,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionRecordData
    {
        public string factionId;
        public string factionDefinitionId;
        public string officialName;
        public string publicDescription;
        public double foundingWorldTime;
        public FactionLifecycleState lifecycleState = FactionLifecycleState.Active;
        public FactionHostContextData hostContext = FactionHostContextData.Independent();
        public string founderPersonId;
        public string founderOrganizationId;
        public string parentFactionId;
        public string[] predecessorFactionIds = Array.Empty<string>();
        public string[] successorFactionIds = Array.Empty<string>();
        public string[] splitFromFactionIds = Array.Empty<string>();
        public string[] mergedFromFactionIds = Array.Empty<string>();
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public bool IsActive => lifecycleState == FactionLifecycleState.Forming || lifecycleState == FactionLifecycleState.Active || lifecycleState == FactionLifecycleState.Suppressed || lifecycleState == FactionLifecycleState.Underground;

        public FactionRecordData Clone()
        {
            return new FactionRecordData
            {
                factionId = factionId ?? string.Empty,
                factionDefinitionId = factionDefinitionId ?? string.Empty,
                officialName = officialName ?? string.Empty,
                publicDescription = publicDescription ?? string.Empty,
                foundingWorldTime = foundingWorldTime,
                lifecycleState = lifecycleState,
                hostContext = hostContext?.Clone() ?? FactionHostContextData.Independent(),
                founderPersonId = founderPersonId ?? string.Empty,
                founderOrganizationId = founderOrganizationId ?? string.Empty,
                parentFactionId = parentFactionId ?? string.Empty,
                predecessorFactionIds = FactionModelUtility.Clean(predecessorFactionIds),
                successorFactionIds = FactionModelUtility.Clean(successorFactionIds),
                splitFromFactionIds = FactionModelUtility.Clean(splitFromFactionIds),
                mergedFromFactionIds = FactionModelUtility.Clean(mergedFromFactionIds),
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = FactionModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionAffiliationRecordData
    {
        public string affiliationId;
        public string factionId;
        public string subjectId;
        public bool subjectIsOrganization;
        public string affiliationDefinitionId;
        public FactionAffiliationStatus status = FactionAffiliationStatus.Active;
        public FactionPublicAlignmentKind publicAlignment = FactionPublicAlignmentKind.PubliclyAligned;
        public FactionPublicAlignmentKind privateAlignment = FactionPublicAlignmentKind.PubliclyAligned;
        public string publicFactionId;
        public string coverFactionId;
        public bool consentRecorded;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string entrySourceId;
        public string acceptanceRecordId;
        public string organizationContextId;
        public string[] factionRoleAssignmentIds = Array.Empty<string>();
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActive => status == FactionAffiliationStatus.Active || status == FactionAffiliationStatus.SecretActive;
        public bool IsEnded => status == FactionAffiliationStatus.Defected || status == FactionAffiliationStatus.Resigned || status == FactionAffiliationStatus.Removed || status == FactionAffiliationStatus.Expelled || status == FactionAffiliationStatus.Former || status == FactionAffiliationStatus.Historical;

        public FactionAffiliationRecordData Clone()
        {
            return new FactionAffiliationRecordData
            {
                affiliationId = affiliationId ?? string.Empty,
                factionId = factionId ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                subjectIsOrganization = subjectIsOrganization,
                affiliationDefinitionId = affiliationDefinitionId ?? string.Empty,
                status = status,
                publicAlignment = publicAlignment,
                privateAlignment = privateAlignment,
                publicFactionId = publicFactionId ?? string.Empty,
                coverFactionId = coverFactionId ?? string.Empty,
                consentRecorded = consentRecorded,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                entrySourceId = entrySourceId ?? string.Empty,
                acceptanceRecordId = acceptanceRecordId ?? string.Empty,
                organizationContextId = organizationContextId ?? string.Empty,
                factionRoleAssignmentIds = FactionModelUtility.Clean(factionRoleAssignmentIds),
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionRoleAssignmentRecordData
    {
        public string roleAssignmentId;
        public string affiliationId;
        public string factionId;
        public string personId;
        public string roleDefinitionId;
        public FactionRoleAssignmentState state = FactionRoleAssignmentState.Active;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string assignmentSourceId;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActive => state == FactionRoleAssignmentState.Active || state == FactionRoleAssignmentState.Acting;

        public FactionRoleAssignmentRecordData Clone()
        {
            return new FactionRoleAssignmentRecordData
            {
                roleAssignmentId = roleAssignmentId ?? string.Empty,
                affiliationId = affiliationId ?? string.Empty,
                factionId = factionId ?? string.Empty,
                personId = personId ?? string.Empty,
                roleDefinitionId = roleDefinitionId ?? string.Empty,
                state = state,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                assignmentSourceId = assignmentSourceId ?? string.Empty,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionPositionRecordData
    {
        public string positionId;
        public string factionId;
        public string positionDefinitionId;
        public FactionPositionTargetKind targetKind = FactionPositionTargetKind.Custom;
        public string targetId;
        public FactionPositionStance stance = FactionPositionStance.Neutral;
        public int weight = 1;
        public int axisValue;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public bool internallyDisputed;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceProposalId;
        public string sourcePolicyId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => endWorldTime < 0d || endWorldTime > worldTime;

        public FactionPositionRecordData Clone()
        {
            return new FactionPositionRecordData
            {
                positionId = positionId ?? string.Empty,
                factionId = factionId ?? string.Empty,
                positionDefinitionId = positionDefinitionId ?? string.Empty,
                targetKind = targetKind,
                targetId = targetId ?? string.Empty,
                stance = stance,
                weight = Math.Max(0, weight),
                axisValue = axisValue,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                internallyDisputed = internallyDisputed,
                visibility = visibility,
                sourceProposalId = sourceProposalId ?? string.Empty,
                sourcePolicyId = sourcePolicyId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionVoteRecommendationRecordData
    {
        public string recommendationId;
        public string factionId;
        public string proposalId;
        public FactionVoteRecommendationKind recommendation = FactionVoteRecommendationKind.Support;
        public double issuedWorldTime;
        public double endWorldTime = -1d;
        public string issuedByPersonId;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceRecordId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => endWorldTime < 0d || endWorldTime > worldTime;

        public FactionVoteRecommendationRecordData Clone()
        {
            return new FactionVoteRecommendationRecordData
            {
                recommendationId = recommendationId ?? string.Empty,
                factionId = factionId ?? string.Empty,
                proposalId = proposalId ?? string.Empty,
                recommendation = recommendation,
                issuedWorldTime = issuedWorldTime,
                endWorldTime = endWorldTime,
                issuedByPersonId = issuedByPersonId ?? string.Empty,
                visibility = visibility,
                sourceRecordId = sourceRecordId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionDispositionRecordData
    {
        public string dispositionId;
        public string sourceFactionId;
        public string targetFactionId;
        public FactionDispositionKind disposition = FactionDispositionKind.Neutral;
        public int intensity;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceRecordId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => endWorldTime < 0d || endWorldTime > worldTime;

        public FactionDispositionRecordData Clone()
        {
            return new FactionDispositionRecordData
            {
                dispositionId = dispositionId ?? string.Empty,
                sourceFactionId = sourceFactionId ?? string.Empty,
                targetFactionId = targetFactionId ?? string.Empty,
                disposition = disposition,
                intensity = Math.Max(-100, Math.Min(100, intensity)),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                visibility = visibility,
                sourceRecordId = sourceRecordId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionStructuralEventRecordData
    {
        public string structuralEventId;
        public string operation;
        public string[] sourceFactionIds = Array.Empty<string>();
        public string[] successorFactionIds = Array.Empty<string>();
        public string sourceRecordId;
        public double worldTime;
        public long revision = 1L;

        public FactionStructuralEventRecordData Clone()
        {
            return new FactionStructuralEventRecordData
            {
                structuralEventId = structuralEventId ?? string.Empty,
                operation = operation ?? string.Empty,
                sourceFactionIds = FactionModelUtility.Clean(sourceFactionIds),
                successorFactionIds = FactionModelUtility.Clean(successorFactionIds),
                sourceRecordId = sourceRecordId ?? string.Empty,
                worldTime = worldTime,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class FactionTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public long revision;

        public FactionTransactionRecordData Clone()
        {
            return new FactionTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class FactionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<FactionRecordData> factions = new List<FactionRecordData>();
        public List<FactionNameRecordData> names = new List<FactionNameRecordData>();
        public List<FactionAffiliationRecordData> affiliations = new List<FactionAffiliationRecordData>();
        public List<FactionRoleAssignmentRecordData> roles = new List<FactionRoleAssignmentRecordData>();
        public List<FactionPositionRecordData> positions = new List<FactionPositionRecordData>();
        public List<FactionVoteRecommendationRecordData> recommendations = new List<FactionVoteRecommendationRecordData>();
        public List<FactionDispositionRecordData> dispositions = new List<FactionDispositionRecordData>();
        public List<FactionStructuralEventRecordData> structuralEvents = new List<FactionStructuralEventRecordData>();
        public List<FactionTransactionRecordData> transactions = new List<FactionTransactionRecordData>();

        public FactionRuntimeSaveData Clone()
        {
            return new FactionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = Math.Max(0L, revision),
                factions = factions == null ? new List<FactionRecordData>() : factions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                names = names == null ? new List<FactionNameRecordData>() : names.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                affiliations = affiliations == null ? new List<FactionAffiliationRecordData>() : affiliations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                roles = roles == null ? new List<FactionRoleAssignmentRecordData>() : roles.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                positions = positions == null ? new List<FactionPositionRecordData>() : positions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                recommendations = recommendations == null ? new List<FactionVoteRecommendationRecordData>() : recommendations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                dispositions = dispositions == null ? new List<FactionDispositionRecordData>() : dispositions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                structuralEvents = structuralEvents == null ? new List<FactionStructuralEventRecordData>() : structuralEvents.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transactions = transactions == null ? new List<FactionTransactionRecordData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class FactionCreateRequest
    {
        public string transactionId;
        public string factionId;
        public string factionDefinitionId;
        public string officialName;
        public string publicDescription;
        public FactionHostContextData hostContext = FactionHostContextData.Independent();
        public string founderPersonId;
        public string founderOrganizationId;
        public string parentFactionId;
        public double worldTime;
        public FactionLifecycleState initialState = FactionLifecycleState.Active;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string[] tags = Array.Empty<string>();
        public bool preview;
    }

    public sealed class FactionAffiliationRequest
    {
        public string transactionId;
        public string affiliationId;
        public string factionId;
        public string personId;
        public string organizationSubjectId;
        public string affiliationDefinitionId;
        public FactionAffiliationStatus targetStatus = FactionAffiliationStatus.Active;
        public FactionPublicAlignmentKind publicAlignment = FactionPublicAlignmentKind.PubliclyAligned;
        public FactionPublicAlignmentKind privateAlignment = FactionPublicAlignmentKind.PubliclyAligned;
        public string publicFactionId;
        public string coverFactionId;
        public bool explicitConsent;
        public string organizationContextId;
        public double worldTime;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceRecordId;
        public bool preview;
    }

    public sealed class FactionRoleAssignmentRequest
    {
        public string transactionId;
        public string roleAssignmentId;
        public string affiliationId;
        public string roleDefinitionId;
        public FactionRoleAssignmentState state = FactionRoleAssignmentState.Active;
        public double worldTime;
        public bool acting;
        public FactionVisibility visibility = FactionVisibility.Public;
        public bool preview;
    }

    public sealed class FactionPositionRequest
    {
        public string transactionId;
        public string positionId;
        public string factionId;
        public string positionDefinitionId;
        public FactionPositionTargetKind targetKind = FactionPositionTargetKind.Custom;
        public string targetId;
        public FactionPositionStance stance = FactionPositionStance.Neutral;
        public int weight = 1;
        public int axisValue;
        public double worldTime;
        public double endWorldTime = -1d;
        public bool internallyDisputed;
        public FactionVisibility visibility = FactionVisibility.Public;
        public string sourceProposalId;
        public string sourcePolicyId;
        public bool preview;
    }

    public sealed class FactionRecommendationRequest
    {
        public string transactionId;
        public string recommendationId;
        public string factionId;
        public string proposalId;
        public FactionVoteRecommendationKind recommendation = FactionVoteRecommendationKind.Support;
        public string issuedByPersonId;
        public double worldTime;
        public double endWorldTime = -1d;
        public FactionVisibility visibility = FactionVisibility.Public;
        public bool preview;
    }

    public sealed class FactionDispositionRequest
    {
        public string transactionId;
        public string dispositionId;
        public string sourceFactionId;
        public string targetFactionId;
        public FactionDispositionKind disposition = FactionDispositionKind.Neutral;
        public int intensity;
        public double worldTime;
        public double endWorldTime = -1d;
        public FactionVisibility visibility = FactionVisibility.Public;
        public bool preview;
    }

    public sealed class FactionLifecycleRequest
    {
        public string transactionId;
        public string factionId;
        public FactionLifecycleState targetState;
        public double worldTime;
        public string successorFactionId;
        public bool preview;
    }

    public sealed class FactionProjectionContext
    {
        public string requesterPersonId;
        public bool developmentView;
        public bool privileged;
    }

    public sealed class FactionOperationResult
    {
        public FactionOperationResult(FactionOperationCode code, string message, long before, long after, bool preview = false, string subjectId = "", FactionRecordData faction = null, FactionAffiliationRecordData affiliation = null, FactionRoleAssignmentRecordData role = null, FactionPositionRecordData position = null, FactionVoteRecommendationRecordData recommendation = null, FactionDispositionRecordData disposition = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
            Preview = preview;
            SubjectId = subjectId ?? string.Empty;
            Faction = faction?.Clone();
            Affiliation = affiliation?.Clone();
            Role = role?.Clone();
            Position = position?.Clone();
            Recommendation = recommendation?.Clone();
            Disposition = disposition?.Clone();
        }

        public FactionOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Preview { get; }
        public string SubjectId { get; }
        public FactionRecordData Faction { get; }
        public FactionAffiliationRecordData Affiliation { get; }
        public FactionRoleAssignmentRecordData Role { get; }
        public FactionPositionRecordData Position { get; }
        public FactionVoteRecommendationRecordData Recommendation { get; }
        public FactionDispositionRecordData Disposition { get; }
        public bool Succeeded => Code == FactionOperationCode.Success || Code == FactionOperationCode.Preview || Code == FactionOperationCode.Duplicate;
    }

    public sealed class FactionEligibilityResult
    {
        public FactionEligibilityResult(bool eligible, string code, string message, bool requiresConsent)
        {
            Eligible = eligible;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RequiresConsent = requiresConsent;
        }

        public bool Eligible { get; }
        public string Code { get; }
        public string Message { get; }
        public bool RequiresConsent { get; }
    }

    public sealed class FactionInfluenceInput
    {
        public FactionInfluenceInput(FactionInfluenceInputKind kind, string sourceId, int value, string explanation)
        {
            Kind = kind;
            SourceId = sourceId ?? string.Empty;
            Value = value;
            Explanation = explanation ?? string.Empty;
        }

        public FactionInfluenceInputKind Kind { get; }
        public string SourceId { get; }
        public int Value { get; }
        public string Explanation { get; }
    }

    public sealed class FactionInfluenceReport
    {
        public FactionInfluenceReport(string factionId, string organizationId, IEnumerable<FactionInfluenceInput> inputs, int uncertainty)
        {
            FactionId = factionId ?? string.Empty;
            OrganizationId = organizationId ?? string.Empty;
            Inputs = (inputs ?? Array.Empty<FactionInfluenceInput>()).OrderBy(item => item.Kind.ToString(), StringComparer.Ordinal).ThenBy(item => item.SourceId, StringComparer.Ordinal).ToArray();
            InfluenceScore = Inputs.Sum(item => item.Value);
            Uncertainty = Math.Max(0, uncertainty);
        }

        public string FactionId { get; }
        public string OrganizationId { get; }
        public IReadOnlyList<FactionInfluenceInput> Inputs { get; }
        public int InfluenceScore { get; }
        public int Uncertainty { get; }
    }

    public sealed class FactionVoteCohesionReport
    {
        public FactionVoteCohesionReport(string factionId, string proposalId, int aligned, int opposed, int abstained, int unknown)
        {
            FactionId = factionId ?? string.Empty;
            ProposalId = proposalId ?? string.Empty;
            AlignedVotes = aligned;
            OpposedVotes = opposed;
            AbstainedVotes = abstained;
            UnknownVotes = unknown;
        }

        public string FactionId { get; }
        public string ProposalId { get; }
        public int AlignedVotes { get; }
        public int OpposedVotes { get; }
        public int AbstainedVotes { get; }
        public int UnknownVotes { get; }
        public int CountedVotes => AlignedVotes + OpposedVotes + AbstainedVotes + UnknownVotes;
    }

    public sealed class FactionProjection
    {
        public FactionProjection(FactionProjectionAccess access, InformationSubjectReferenceData subject, FactionRecordData faction, IReadOnlyList<FactionAffiliationRecordData> affiliations, IReadOnlyList<FactionPositionRecordData> positions, string message)
        {
            Access = access;
            Subject = subject?.Clone() ?? new InformationSubjectReferenceData();
            Faction = faction?.Clone();
            Affiliations = (affiliations ?? Array.Empty<FactionAffiliationRecordData>()).Select(item => item.Clone()).ToArray();
            Positions = (positions ?? Array.Empty<FactionPositionRecordData>()).Select(item => item.Clone()).ToArray();
            Message = message ?? string.Empty;
        }

        public FactionProjectionAccess Access { get; }
        public InformationSubjectReferenceData Subject { get; }
        public FactionRecordData Faction { get; }
        public IReadOnlyList<FactionAffiliationRecordData> Affiliations { get; }
        public IReadOnlyList<FactionPositionRecordData> Positions { get; }
        public string Message { get; }
        public bool Succeeded => Access == FactionProjectionAccess.Full || Access == FactionProjectionAccess.Redacted || Access == FactionProjectionAccess.Development;
        public bool Redacted => Access == FactionProjectionAccess.Redacted;
    }
}
