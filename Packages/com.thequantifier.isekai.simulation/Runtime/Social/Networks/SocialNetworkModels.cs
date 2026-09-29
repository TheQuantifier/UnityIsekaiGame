using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Social.Networks
{
    [Serializable]
    public sealed class SocialGraphEdgeSourceWeightData
    {
        public SocialGraphEdgeKind edgeKind;
        public int authoredWeight = 50;
        public int positiveThreshold = 1;
        public int negativeThreshold = -1;
        public bool includeHistorical;

        public SocialGraphEdgeSourceWeightData Clone() => new SocialGraphEdgeSourceWeightData { edgeKind = edgeKind, authoredWeight = authoredWeight, positiveThreshold = positiveThreshold, negativeThreshold = negativeThreshold, includeHistorical = includeHistorical };
    }

    [Serializable]
    public sealed class SocialNetworkGroupRoleDefinitionData
    {
        public string roleId;
        public string displayName;
        public bool leaderRole;

        public SocialNetworkGroupRoleDefinitionData Clone() => new SocialNetworkGroupRoleDefinitionData { roleId = roleId ?? string.Empty, displayName = displayName ?? string.Empty, leaderRole = leaderRole };
    }

    [Serializable]
    public sealed class SocialGroupRecordData
    {
        public string groupId;
        public string groupDefinitionId;
        public string displayName;
        public InformalSocialGroupLifecycleStatus lifecycle = InformalSocialGroupLifecycleStatus.Active;
        public string audienceId;
        public string sourceCandidateId;
        public string sourceProjectionDefinitionId;
        public double createdWorldTime;
        public double dissolvedWorldTime = -1d;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public SocialGroupRecordData Clone() => new SocialGroupRecordData
        {
            groupId = groupId ?? string.Empty,
            groupDefinitionId = groupDefinitionId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            lifecycle = lifecycle,
            audienceId = audienceId ?? string.Empty,
            sourceCandidateId = sourceCandidateId ?? string.Empty,
            sourceProjectionDefinitionId = sourceProjectionDefinitionId ?? string.Empty,
            createdWorldTime = createdWorldTime,
            dissolvedWorldTime = dissolvedWorldTime,
            tags = Clean(tags),
            revision = revision
        };

        public static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialGroupMembershipRecordData
    {
        public string membershipId;
        public string groupId;
        public string personId;
        public string roleId;
        public SocialGroupMembershipStatus status = SocialGroupMembershipStatus.Active;
        public double joinedWorldTime;
        public double endedWorldTime = -1d;
        public string sourceRecordId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public SocialGroupMembershipRecordData Clone() => new SocialGroupMembershipRecordData
        {
            membershipId = membershipId ?? string.Empty,
            groupId = groupId ?? string.Empty,
            personId = personId ?? string.Empty,
            roleId = roleId ?? string.Empty,
            status = status,
            joinedWorldTime = joinedWorldTime,
            endedWorldTime = endedWorldTime,
            sourceRecordId = sourceRecordId ?? string.Empty,
            tags = SocialGroupRecordData.Clean(tags),
            revision = revision
        };
    }

    [Serializable]
    public sealed class SocialNetworkProcessedTransactionData
    {
        public string transactionId;
        public SocialNetworkOperationStatus status;
        public string groupId;
        public string membershipId;
        public long revision;

        public SocialNetworkProcessedTransactionData Clone() => new SocialNetworkProcessedTransactionData { transactionId = transactionId ?? string.Empty, status = status, groupId = groupId ?? string.Empty, membershipId = membershipId ?? string.Empty, revision = revision };
    }

    [Serializable]
    public sealed class SocialNetworkRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<SocialGroupRecordData> groups = new List<SocialGroupRecordData>();
        public List<SocialGroupMembershipRecordData> memberships = new List<SocialGroupMembershipRecordData>();
        public List<SocialNetworkProcessedTransactionData> processedTransactions = new List<SocialNetworkProcessedTransactionData>();

        public SocialNetworkRuntimeSaveData Clone() => new SocialNetworkRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            revision = revision,
            groups = groups == null ? new List<SocialGroupRecordData>() : groups.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            memberships = memberships == null ? new List<SocialGroupMembershipRecordData>() : memberships.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            processedTransactions = processedTransactions == null ? new List<SocialNetworkProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
        };
    }

    public sealed class SocialGroupMutationRequest
    {
        public string TransactionId { get; set; }
        public SocialGroupMutationKind MutationKind { get; set; }
        public string GroupId { get; set; }
        public string GroupDefinitionId { get; set; }
        public string DisplayName { get; set; }
        public string MembershipId { get; set; }
        public string PersonId { get; set; }
        public string RoleId { get; set; }
        public string AudienceId { get; set; }
        public string SourceRecordId { get; set; }
        public string SourceCandidateId { get; set; }
        public string SourceProjectionDefinitionId { get; set; }
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
        public double WorldTime { get; set; }
        public bool Preview { get; set; }
    }

    public sealed class SocialGroupSnapshot
    {
        public SocialGroupSnapshot(SocialGroupRecordData data) { Data = data?.Clone() ?? new SocialGroupRecordData(); }
        public SocialGroupRecordData Data { get; }
        public string GroupId => Data.groupId ?? string.Empty;
        public string GroupDefinitionId => Data.groupDefinitionId ?? string.Empty;
        public InformalSocialGroupLifecycleStatus Lifecycle => Data.lifecycle;
        public IReadOnlyList<string> Tags => Data.tags ?? Array.Empty<string>();
        public long Revision => Data.revision;
    }

    public sealed class SocialGroupMembershipSnapshot
    {
        public SocialGroupMembershipSnapshot(SocialGroupMembershipRecordData data) { Data = data?.Clone() ?? new SocialGroupMembershipRecordData(); }
        public SocialGroupMembershipRecordData Data { get; }
        public string MembershipId => Data.membershipId ?? string.Empty;
        public string GroupId => Data.groupId ?? string.Empty;
        public string PersonId => Data.personId ?? string.Empty;
        public string RoleId => Data.roleId ?? string.Empty;
        public SocialGroupMembershipStatus Status => Data.status;
        public bool Active => Status == SocialGroupMembershipStatus.Active;
    }

    public sealed class SocialNetworkMutationResult
    {
        private SocialNetworkMutationResult(bool succeeded, SocialNetworkOperationStatus status, string message, string transactionId, bool preview, bool duplicate, SocialGroupSnapshot group, SocialGroupMembershipSnapshot membership, long beforeRevision, long afterRevision)
        {
            Succeeded = succeeded; Status = status; Message = message ?? string.Empty; TransactionId = transactionId ?? string.Empty; Preview = preview; Duplicate = duplicate; Group = group; Membership = membership; BeforeRevision = beforeRevision; AfterRevision = afterRevision;
        }
        public bool Succeeded { get; }
        public SocialNetworkOperationStatus Status { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public SocialGroupSnapshot Group { get; }
        public SocialGroupMembershipSnapshot Membership { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public static SocialNetworkMutationResult Success(SocialNetworkOperationStatus status, string message, string transactionId, SocialGroupSnapshot group, SocialGroupMembershipSnapshot membership, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false) => new SocialNetworkMutationResult(true, status, message, transactionId, preview, duplicate, group, membership, beforeRevision, afterRevision);
        public static SocialNetworkMutationResult Failure(SocialNetworkOperationStatus status, string message, string transactionId = "", long revision = 0L) => new SocialNetworkMutationResult(false, status, message, transactionId, false, false, null, null, revision, revision);
    }

    [Serializable]
    public sealed class SocialGraphRevisionDependenciesData
    {
        public long relationshipRevision;
        public long attitudeRevision;
        public long reputationRevision;
        public long rumorRevision;
        public long interactionRevision;
        public long normRevision;
        public long groupRevision;
        public SocialGraphRevisionDependenciesData Clone() => (SocialGraphRevisionDependenciesData)MemberwiseClone();
    }

    [Serializable]
    public sealed class SocialGraphNodeData
    {
        public string nodeId;
        public SocialGraphNodeKind kind = SocialGraphNodeKind.Person;
        public bool isolated;
        public string[] diagnostics = Array.Empty<string>();
        public SocialGraphNodeData Clone() => new SocialGraphNodeData { nodeId = nodeId ?? string.Empty, kind = kind, isolated = isolated, diagnostics = SocialGroupRecordData.Clean(diagnostics) };
    }

    [Serializable]
    public sealed class SocialGraphEdgeData
    {
        public string edgeId;
        public string sourcePersonId;
        public string destinationPersonId;
        public SocialGraphEdgeKind edgeKind;
        public bool directed;
        public string[] sourceRecordIds = Array.Empty<string>();
        public string[] definitionOrDimensionIds = Array.Empty<string>();
        public int rawValue;
        public int normalizedWeight;
        public SocialGraphValence valence;
        public double worldTime;
        public SocialGraphVisibility visibility;
        public string projectionDefinitionId;
        public string explanation;

        public SocialGraphEdgeData Clone() => new SocialGraphEdgeData
        {
            edgeId = edgeId ?? string.Empty,
            sourcePersonId = sourcePersonId ?? string.Empty,
            destinationPersonId = destinationPersonId ?? string.Empty,
            edgeKind = edgeKind,
            directed = directed,
            sourceRecordIds = SocialGroupRecordData.Clean(sourceRecordIds),
            definitionOrDimensionIds = SocialGroupRecordData.Clean(definitionOrDimensionIds),
            rawValue = rawValue,
            normalizedWeight = normalizedWeight,
            valence = valence,
            worldTime = worldTime,
            visibility = visibility,
            projectionDefinitionId = projectionDefinitionId ?? string.Empty,
            explanation = explanation ?? string.Empty
        };
    }

    public sealed class SocialGraphNodeSnapshot
    {
        private readonly SocialGraphNodeData data;

        public SocialGraphNodeSnapshot(SocialGraphNodeData data)
        {
            this.data = data?.Clone() ?? new SocialGraphNodeData();
        }

        public SocialGraphNodeData Data => data.Clone();
        public string NodeId => data.nodeId ?? string.Empty;
        public bool Isolated => data.isolated;
    }

    public sealed class SocialGraphEdgeSnapshot
    {
        private readonly SocialGraphEdgeData data;

        public SocialGraphEdgeSnapshot(SocialGraphEdgeData data)
        {
            this.data = data?.Clone() ?? new SocialGraphEdgeData();
        }

        public SocialGraphEdgeData Data => data.Clone();
        public string EdgeId => data.edgeId ?? string.Empty;
        public string SourcePersonId => data.sourcePersonId ?? string.Empty;
        public string DestinationPersonId => data.destinationPersonId ?? string.Empty;
        public SocialGraphEdgeKind EdgeKind => data.edgeKind;
        public int Weight => data.normalizedWeight;
        public SocialGraphValence Valence => data.valence;
        public IReadOnlyList<string> SourceRecordIds => SocialGroupRecordData.Clean(data.sourceRecordIds);
    }

    public sealed class SocialGraphSnapshot
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<SocialGraphEdgeSnapshot>> outgoing;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<SocialGraphEdgeSnapshot>> incoming;

        public SocialGraphSnapshot(string projectionDefinitionId, double snapshotWorldTime, IReadOnlyList<SocialGraphNodeData> nodes, IReadOnlyList<SocialGraphEdgeData> edges, SocialGraphRevisionDependenciesData dependencies, bool truncated, string diagnostics)
        {
            ProjectionDefinitionId = projectionDefinitionId ?? string.Empty;
            SnapshotWorldTime = snapshotWorldTime;
            Nodes = (nodes ?? Array.Empty<SocialGraphNodeData>()).Select(item => new SocialGraphNodeSnapshot(item)).OrderBy(item => item.NodeId, StringComparer.Ordinal).ToArray();
            Edges = (edges ?? Array.Empty<SocialGraphEdgeData>()).Select(item => new SocialGraphEdgeSnapshot(item)).OrderBy(item => item.SourcePersonId, StringComparer.Ordinal).ThenBy(item => item.DestinationPersonId, StringComparer.Ordinal).ThenBy(item => item.EdgeKind).ThenBy(item => item.EdgeId, StringComparer.Ordinal).ToArray();
            RevisionDependencies = dependencies?.Clone() ?? new SocialGraphRevisionDependenciesData();
            Truncated = truncated;
            Diagnostics = diagnostics ?? string.Empty;
            outgoing = Edges.GroupBy(item => item.SourcePersonId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<SocialGraphEdgeSnapshot>)group.ToArray(), StringComparer.Ordinal);
            incoming = Edges.GroupBy(item => item.DestinationPersonId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<SocialGraphEdgeSnapshot>)group.ToArray(), StringComparer.Ordinal);
        }

        public string ProjectionDefinitionId { get; }
        public double SnapshotWorldTime { get; }
        public IReadOnlyList<SocialGraphNodeSnapshot> Nodes { get; }
        public IReadOnlyList<SocialGraphEdgeSnapshot> Edges { get; }
        public SocialGraphRevisionDependenciesData RevisionDependencies { get; }
        public bool Truncated { get; }
        public string Diagnostics { get; }
        public IReadOnlyList<SocialGraphEdgeSnapshot> Outgoing(string personId) => outgoing.TryGetValue(personId ?? string.Empty, out IReadOnlyList<SocialGraphEdgeSnapshot> edges) ? edges.ToArray() : Array.Empty<SocialGraphEdgeSnapshot>();
        public IReadOnlyList<SocialGraphEdgeSnapshot> Incoming(string personId) => incoming.TryGetValue(personId ?? string.Empty, out IReadOnlyList<SocialGraphEdgeSnapshot> edges) ? edges.ToArray() : Array.Empty<SocialGraphEdgeSnapshot>();
        public IReadOnlyList<SocialGraphEdgeSnapshot> Incident(string personId) => Outgoing(personId).Concat(Incoming(personId)).OrderBy(item => item.SourcePersonId, StringComparer.Ordinal).ThenBy(item => item.DestinationPersonId, StringComparer.Ordinal).ThenBy(item => item.EdgeKind).ToArray();
    }

    public sealed class SocialGraphQueryRequest
    {
        public string ProjectionDefinitionId { get; set; }
        public double WorldTime { get; set; }
        public int MaxDepth { get; set; } = 3;
        public int MaxVisitedNodes { get; set; } = 64;
        public int MaxReturnedPaths { get; set; } = 8;
        public int MinimumWeight { get; set; }
        public IReadOnlyList<SocialGraphEdgeKind> EdgeKinds { get; set; } = Array.Empty<SocialGraphEdgeKind>();
        public SocialGraphVisibility Visibility { get; set; } = SocialGraphVisibility.Authoritative;
    }

    public sealed class SocialGraphNeighborResult { public string PersonId { get; set; } public IReadOnlyList<SocialGraphEdgeSnapshot> Edges { get; set; } = Array.Empty<SocialGraphEdgeSnapshot>(); public int Rank { get; set; } public int EffectiveWeight { get; set; } public string Explanation { get; set; } = string.Empty; }
    public sealed class SocialGraphMutualConnectionResult { public string MutualPersonId { get; set; } public IReadOnlyList<SocialGraphEdgeSnapshot> FirstEdges { get; set; } = Array.Empty<SocialGraphEdgeSnapshot>(); public IReadOnlyList<SocialGraphEdgeSnapshot> SecondEdges { get; set; } = Array.Empty<SocialGraphEdgeSnapshot>(); public int EffectiveWeight { get; set; } }
    public sealed class SocialGraphPathResult { public bool Connected { get; set; } public int Distance { get; set; } = -1; public string[] PersonPath { get; set; } = Array.Empty<string>(); public bool Truncated { get; set; } public string Diagnostics { get; set; } = string.Empty; }
    public sealed class SocialGraphMetricsResult { public string PersonId { get; set; } public int Degree { get; set; } public int IncomingDegree { get; set; } public int OutgoingDegree { get; set; } public int WeightedDegree { get; set; } public int PositiveDegree { get; set; } public int NegativeDegree { get; set; } public int MutualConnectionCount { get; set; } public float LocalDensity { get; set; } public float IsolationScore { get; set; } public int BoundedBridgeScore { get; set; } public bool Approximate { get; set; } public bool Truncated { get; set; } }
    public sealed class SocialGraphComponentResult { public string ComponentKey { get; set; } public string[] MemberPersonIds { get; set; } = Array.Empty<string>(); public int EdgeCount { get; set; } public float Density { get; set; } public bool Truncated { get; set; } }
    public sealed class SocialGraphCliqueCandidate { public string CandidateId { get; set; } public string ProjectionDefinitionId { get; set; } public string[] MemberPersonIds { get; set; } = Array.Empty<string>(); public string[] SourceEdgeIds { get; set; } = Array.Empty<string>(); public int Score { get; set; } public bool Maximal { get; set; } public bool Truncated { get; set; } }
    public sealed class SocialGraphCommunityCandidate { public string CandidateId { get; set; } public string ProjectionDefinitionId { get; set; } public string[] MemberPersonIds { get; set; } = Array.Empty<string>(); public int InternalEdgeCount { get; set; } public float Density { get; set; } public bool Truncated { get; set; } public string Algorithm { get; set; } = string.Empty; }
    public sealed class SocialGroupMetricsResult { public string GroupId { get; set; } public int ActiveMemberCount { get; set; } public int HistoricalMemberCount { get; set; } public float InternalDensity { get; set; } public int PositiveCohesion { get; set; } public int HostilityConflict { get; set; } public string[] IsolatedMembers { get; set; } = Array.Empty<string>(); public string[] BridgeMembers { get; set; } = Array.Empty<string>(); public bool MutatedGroup { get; set; } }
}
