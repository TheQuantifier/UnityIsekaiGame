using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Networks
{
    public sealed class SocialNetworkRuntime : IDisposable
    {
        private readonly Dictionary<string, SocialGroupRecordData> groupsById = new Dictionary<string, SocialGroupRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialGroupMembershipRecordData> membershipsById = new Dictionary<string, SocialGroupMembershipRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialNetworkProcessedTransactionData> processedTransactions = new Dictionary<string, SocialNetworkProcessedTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialGraphSnapshot> graphCache = new Dictionary<string, SocialGraphSnapshot>(StringComparer.Ordinal);
        private readonly HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private RelationshipRuntime relationships;
        private InterpersonalAttitudeRuntime attitudes;
        private ReputationRuntime reputation;
        private RumorRuntime rumors;
        private SocialInteractionRuntime interactions;
        private SocialNormRuntime norms;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public int GroupCount => groupsById.Count;
        public int MembershipCount => membershipsById.Count;

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> persons,
            RelationshipRuntime relationshipRuntime,
            InterpersonalAttitudeRuntime attitudeRuntime,
            ReputationRuntime reputationRuntime,
            RumorRuntime rumorRuntime,
            SocialInteractionRuntime interactionRuntime,
            SocialNormRuntime normRuntime)
        {
            registry = definitionRegistry;
            relationships = relationshipRuntime;
            attitudes = attitudeRuntime;
            reputation = reputationRuntime;
            rumors = rumorRuntime;
            interactions = interactionRuntime;
            norms = normRuntime;
            knownPersonIds.Clear();
            foreach (string person in Clean(persons))
            {
                knownPersonIds.Add(person);
            }

            graphCache.Clear();
        }

        public SocialNetworkMutationResult Mutate(SocialGroupMutationRequest request)
        {
            if (disposed)
            {
                return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.RuntimeNotReady, "Social Network runtime is disposed.", request?.TransactionId, Revision);
            }

            if (request == null)
            {
                return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRequest, "Social network request is missing.", revision: Revision);
            }

            string transactionId = request.TransactionId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRequest, "Social network transaction ID is required.", revision: Revision);
            }

            if (!request.Preview && processedTransactions.TryGetValue(transactionId, out SocialNetworkProcessedTransactionData processed))
            {
                SocialGroupSnapshot group = string.IsNullOrWhiteSpace(processed.groupId) || !groupsById.TryGetValue(processed.groupId, out SocialGroupRecordData groupData) ? null : new SocialGroupSnapshot(groupData);
                SocialGroupMembershipSnapshot membership = string.IsNullOrWhiteSpace(processed.membershipId) || !membershipsById.TryGetValue(processed.membershipId, out SocialGroupMembershipRecordData membershipData) ? null : new SocialGroupMembershipSnapshot(membershipData);
                return SocialNetworkMutationResult.Success(SocialNetworkOperationStatus.Duplicate, "Social network transaction already processed.", transactionId, group, membership, Revision, Revision, duplicate: true);
            }

            long before = Revision;
            SocialNetworkRuntimeSaveData rollback = request.Preview ? null : CreateSaveData();
            SocialNetworkMutationResult result = request.MutationKind switch
            {
                SocialGroupMutationKind.CreateGroup => CreateGroup(request, before),
                SocialGroupMutationKind.CreateGroupFromCandidate => CreateGroup(request, before),
                SocialGroupMutationKind.AddMembership => AddMembership(request, before),
                SocialGroupMutationKind.ChangeMembershipRole => ChangeMembershipRole(request, before),
                SocialGroupMutationKind.EndMembership => EndMembership(request, before),
                SocialGroupMutationKind.DissolveGroup => DissolveGroup(request, before),
                _ => SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRequest, $"Unsupported social network mutation '{request.MutationKind}'.", transactionId, before)
            };

            if (!result.Succeeded || request.Preview)
            {
                if (request.Preview && rollback != null)
                {
                    RestoreInternal(rollback);
                }
                return result;
            }

            if (!ValidateSaveData(CreateSaveData(), registry, knownPersonIds, out string failure))
            {
                RestoreInternal(rollback);
                return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.ValidationFailed, failure, transactionId, before);
            }

            Revision++;
            IsDirty = true;
            StampResult(result, Revision);
            processedTransactions[transactionId] = new SocialNetworkProcessedTransactionData { transactionId = transactionId, status = SocialNetworkOperationStatus.Succeeded, groupId = result.Group?.GroupId ?? string.Empty, membershipId = result.Membership?.MembershipId ?? string.Empty, revision = Revision };
            graphCache.Clear();
            return SocialNetworkMutationResult.Success(SocialNetworkOperationStatus.Succeeded, result.Message, transactionId, Snapshot(result.Group?.GroupId), SnapshotMembership(result.Membership?.MembershipId), before, Revision);
        }

        public SocialGraphSnapshot BuildGraph(SocialGraphQueryRequest request)
        {
            SocialGraphProjectionDefinition definition = ResolveProjection(request?.ProjectionDefinitionId);
            if (definition == null)
            {
                return new SocialGraphSnapshot(request?.ProjectionDefinitionId ?? string.Empty, request?.WorldTime ?? 0d, Array.Empty<SocialGraphNodeData>(), Array.Empty<SocialGraphEdgeData>(), CaptureDependencies(), false, "Projection definition is missing.");
            }

            SocialGraphRevisionDependenciesData dependencies = CaptureDependencies();
            string cacheKey = CacheKey(definition, request, dependencies);
            if (graphCache.TryGetValue(cacheKey, out SocialGraphSnapshot cached))
            {
                return cached;
            }

            List<SocialGraphNodeData> nodes = Clean(knownPersonIds).Select(person => new SocialGraphNodeData { nodeId = person }).ToList();
            List<SocialGraphEdgeData> edges = new List<SocialGraphEdgeData>();
            AddRelationshipEdges(definition, request, edges);
            AddAttitudeEdges(definition, request, edges);
            AddInteractionEdges(definition, request, edges);
            AddRumorEdges(definition, request, edges);
            AddGroupEdges(definition, request, edges);
            edges = ApplyRequestFilters(definition, request, edges);

            HashSet<string> edgePeople = new HashSet<string>(edges.SelectMany(edge => new[] { edge.sourcePersonId, edge.destinationPersonId }).Where(item => !string.IsNullOrWhiteSpace(item)), StringComparer.Ordinal);
            foreach (string person in edgePeople.OrderBy(item => item, StringComparer.Ordinal))
            {
                if (nodes.All(node => !string.Equals(node.nodeId, person, StringComparison.Ordinal)))
                {
                    nodes.Add(new SocialGraphNodeData { nodeId = person });
                }
            }

            foreach (SocialGraphNodeData node in nodes)
            {
                node.isolated = edges.All(edge => !string.Equals(edge.sourcePersonId, node.nodeId, StringComparison.Ordinal) && !string.Equals(edge.destinationPersonId, node.nodeId, StringComparison.Ordinal));
            }

            bool truncated = false;
            if (nodes.Count > definition.MaximumNodes)
            {
                nodes = nodes.OrderBy(item => item.nodeId, StringComparer.Ordinal).Take(definition.MaximumNodes).ToList();
                HashSet<string> allowed = new HashSet<string>(nodes.Select(item => item.nodeId), StringComparer.Ordinal);
                edges = edges.Where(edge => allowed.Contains(edge.sourcePersonId) && allowed.Contains(edge.destinationPersonId)).ToList();
                truncated = true;
            }

            if (edges.Count > definition.MaximumEdges)
            {
                edges = OrderedEdges(edges).Take(definition.MaximumEdges).ToList();
                truncated = true;
            }

            SocialGraphSnapshot snapshot = new SocialGraphSnapshot(definition.Id, request?.WorldTime ?? 0d, nodes, OrderedEdges(edges).ToArray(), dependencies, truncated, truncated ? "Projection truncated by configured limits." : "Projection built from authoritative social runtimes.");
            graphCache[cacheKey] = snapshot;
            return snapshot;
        }

        public IReadOnlyList<SocialGraphNeighborResult> QueryNeighbors(string personId, SocialGraphQueryRequest request, bool outgoing = true, bool incoming = true)
        {
            SocialGraphSnapshot graph = BuildGraph(request);
            IEnumerable<SocialGraphEdgeSnapshot> edges = Array.Empty<SocialGraphEdgeSnapshot>();
            if (outgoing) edges = edges.Concat(graph.Outgoing(personId));
            if (incoming) edges = edges.Concat(graph.Incoming(personId));
            return edges.GroupBy(edge => string.Equals(edge.SourcePersonId, personId, StringComparison.Ordinal) ? edge.DestinationPersonId : edge.SourcePersonId, StringComparer.Ordinal)
                .Select(group => new SocialGraphNeighborResult { PersonId = group.Key, Edges = group.OrderBy(item => item.EdgeKind).ThenBy(item => item.EdgeId, StringComparer.Ordinal).ToArray(), EffectiveWeight = group.Sum(item => item.Weight), Explanation = string.Join("; ", group.Select(item => item.Data.explanation).Distinct(StringComparer.Ordinal)) })
                .OrderByDescending(item => item.EffectiveWeight)
                .ThenBy(item => item.PersonId, StringComparer.Ordinal)
                .Select((item, index) => { item.Rank = index + 1; return item; })
                .ToArray();
        }

        public IReadOnlyList<SocialGraphMutualConnectionResult> QueryMutualConnections(string firstPersonId, string secondPersonId, SocialGraphQueryRequest request)
        {
            IReadOnlyDictionary<string, SocialGraphNeighborResult> first = QueryNeighbors(firstPersonId, request).ToDictionary(item => item.PersonId, StringComparer.Ordinal);
            IReadOnlyDictionary<string, SocialGraphNeighborResult> second = QueryNeighbors(secondPersonId, request).ToDictionary(item => item.PersonId, StringComparer.Ordinal);
            return first.Keys.Intersect(second.Keys, StringComparer.Ordinal)
                .Where(person => !string.Equals(person, firstPersonId, StringComparison.Ordinal) && !string.Equals(person, secondPersonId, StringComparison.Ordinal))
                .OrderBy(person => person, StringComparer.Ordinal)
                .Select(person => new SocialGraphMutualConnectionResult { MutualPersonId = person, FirstEdges = first[person].Edges, SecondEdges = second[person].Edges, EffectiveWeight = first[person].EffectiveWeight + second[person].EffectiveWeight })
                .OrderByDescending(item => item.EffectiveWeight)
                .ThenBy(item => item.MutualPersonId, StringComparer.Ordinal)
                .ToArray();
        }

        public SocialGraphPathResult FindShortestPath(string sourcePersonId, string destinationPersonId, SocialGraphQueryRequest request)
        {
            SocialGraphSnapshot graph = BuildGraph(request);
            int maxDepth = Math.Min(Math.Max(1, request?.MaxDepth ?? 3), ResolveProjection(request?.ProjectionDefinitionId)?.MaximumTraversalDepth ?? 4);
            int maxVisited = Math.Max(1, request?.MaxVisitedNodes ?? 64);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Queue<string[]> queue = new Queue<string[]>();
            queue.Enqueue(new[] { sourcePersonId });
            visited.Add(sourcePersonId);
            bool truncated = false;
            while (queue.Count > 0)
            {
                string[] path = queue.Dequeue();
                string current = path[path.Length - 1];
                if (string.Equals(current, destinationPersonId, StringComparison.Ordinal))
                {
                    return new SocialGraphPathResult { Connected = true, Distance = path.Length - 1, PersonPath = path.ToArray(), Truncated = truncated, Diagnostics = "Shortest path found by deterministic breadth-first traversal." };
                }
                if (path.Length - 1 >= maxDepth) continue;
                foreach (string neighbor in QueryNeighbors(current, request).Select(item => item.PersonId).OrderBy(item => item, StringComparer.Ordinal))
                {
                    if (visited.Count >= maxVisited)
                    {
                        truncated = true;
                        continue;
                    }
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(path.Concat(new[] { neighbor }).ToArray());
                    }
                }
            }
            return new SocialGraphPathResult { Connected = false, Distance = -1, PersonPath = Array.Empty<string>(), Truncated = truncated, Diagnostics = truncated ? "Traversal stopped at configured visited-node limit." : "No path found within configured limits." };
        }

        public SocialGraphMetricsResult CalculatePersonMetrics(string personId, SocialGraphQueryRequest request)
        {
            SocialGraphSnapshot graph = BuildGraph(request);
            IReadOnlyList<SocialGraphEdgeSnapshot> incomingEdges = graph.Incoming(personId);
            IReadOnlyList<SocialGraphEdgeSnapshot> outgoingEdges = graph.Outgoing(personId);
            IReadOnlyList<SocialGraphNeighborResult> neighbors = QueryNeighbors(personId, request);
            int possibleNeighborLinks = neighbors.Count * Math.Max(0, neighbors.Count - 1);
            int neighborInternalEdges = possibleNeighborLinks == 0 ? 0 : graph.Edges.Count(edge => neighbors.Any(item => item.PersonId == edge.SourcePersonId) && neighbors.Any(item => item.PersonId == edge.DestinationPersonId));
            return new SocialGraphMetricsResult
            {
                PersonId = personId ?? string.Empty,
                Degree = neighbors.Count,
                IncomingDegree = incomingEdges.Count,
                OutgoingDegree = outgoingEdges.Count,
                WeightedDegree = incomingEdges.Concat(outgoingEdges).Sum(edge => edge.Weight),
                PositiveDegree = incomingEdges.Concat(outgoingEdges).Count(edge => edge.Valence == SocialGraphValence.Positive),
                NegativeDegree = incomingEdges.Concat(outgoingEdges).Count(edge => edge.Valence == SocialGraphValence.Negative),
                MutualConnectionCount = neighbors.Count(item => graph.Outgoing(item.PersonId).Any(edge => edge.DestinationPersonId == personId) || graph.Incoming(item.PersonId).Any(edge => edge.SourcePersonId == personId)),
                LocalDensity = possibleNeighborLinks == 0 ? 0f : (float)neighborInternalEdges / possibleNeighborLinks,
                IsolationScore = neighbors.Count == 0 ? 1f : 0f,
                BoundedBridgeScore = neighbors.Count(item => QueryNeighbors(item.PersonId, request).Count <= 1),
                Approximate = true,
                Truncated = graph.Truncated
            };
        }

        public IReadOnlyList<SocialGraphComponentResult> FindConnectedComponents(SocialGraphQueryRequest request)
        {
            SocialGraphSnapshot graph = BuildGraph(request);
            HashSet<string> unvisited = new HashSet<string>(graph.Nodes.Select(item => item.NodeId), StringComparer.Ordinal);
            List<SocialGraphComponentResult> components = new List<SocialGraphComponentResult>();
            int index = 0;
            while (unvisited.Count > 0)
            {
                string start = unvisited.OrderBy(item => item, StringComparer.Ordinal).First();
                Queue<string> queue = new Queue<string>();
                List<string> members = new List<string>();
                queue.Enqueue(start);
                unvisited.Remove(start);
                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    members.Add(current);
                    foreach (string neighbor in QueryNeighbors(current, request).Select(item => item.PersonId).OrderBy(item => item, StringComparer.Ordinal))
                    {
                        if (unvisited.Remove(neighbor)) queue.Enqueue(neighbor);
                    }
                }
                int edgeCount = graph.Edges.Count(edge => members.Contains(edge.SourcePersonId) && members.Contains(edge.DestinationPersonId));
                int possible = members.Count * Math.Max(0, members.Count - 1);
                components.Add(new SocialGraphComponentResult { ComponentKey = $"component.{index:D3}.{string.Join("-", members.OrderBy(item => item, StringComparer.Ordinal).Take(3))}", MemberPersonIds = members.OrderBy(item => item, StringComparer.Ordinal).ToArray(), EdgeCount = edgeCount, Density = possible == 0 ? 0f : (float)edgeCount / possible, Truncated = graph.Truncated });
                index++;
            }
            return components.OrderByDescending(item => item.MemberPersonIds.Length).ThenBy(item => item.ComponentKey, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<SocialGraphCliqueCandidate> FindCliqueCandidates(SocialGraphQueryRequest request)
        {
            SocialGraphProjectionDefinition definition = ResolveProjection(request?.ProjectionDefinitionId);
            SocialGraphSnapshot graph = BuildGraph(request);
            int maxNodes = Math.Min(definition?.MaximumAnalysisNodes ?? 64, graph.Nodes.Count);
            int maxResults = definition?.MaximumAnalysisResults ?? 16;
            string[] nodes = graph.Nodes.Select(item => item.NodeId).OrderBy(item => item, StringComparer.Ordinal).Take(maxNodes).ToArray();
            HashSet<string> undirected = new HashSet<string>(graph.Edges.Select(edge => PairKey(edge.SourcePersonId, edge.DestinationPersonId)), StringComparer.Ordinal);
            List<SocialGraphCliqueCandidate> candidates = new List<SocialGraphCliqueCandidate>();
            for (int i = 0; i < nodes.Length; i++)
            for (int j = i + 1; j < nodes.Length; j++)
            for (int k = j + 1; k < nodes.Length; k++)
            {
                string[] triad = { nodes[i], nodes[j], nodes[k] };
                if (AllConnected(triad, undirected))
                {
                    string[] edgeIds = graph.Edges.Where(edge => triad.Contains(edge.SourcePersonId) && triad.Contains(edge.DestinationPersonId)).Select(edge => edge.EdgeId).OrderBy(item => item, StringComparer.Ordinal).ToArray();
                    candidates.Add(new SocialGraphCliqueCandidate { CandidateId = $"clique.{request?.ProjectionDefinitionId}.{string.Join(".", triad)}", ProjectionDefinitionId = request?.ProjectionDefinitionId ?? string.Empty, MemberPersonIds = triad, SourceEdgeIds = edgeIds, Score = edgeIds.Length, Maximal = true, Truncated = graph.Truncated || candidates.Count + 1 >= maxResults });
                    if (candidates.Count >= maxResults) return candidates.ToArray();
                }
            }
            return candidates.OrderByDescending(item => item.Score).ThenBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<SocialGraphCommunityCandidate> FindCommunityCandidates(SocialGraphQueryRequest request)
        {
            return FindConnectedComponents(request)
                .Where(item => item.MemberPersonIds.Length >= 2)
                .Take(ResolveProjection(request?.ProjectionDefinitionId)?.MaximumAnalysisResults ?? 16)
                .Select(item => new SocialGraphCommunityCandidate { CandidateId = $"community.{item.ComponentKey}", ProjectionDefinitionId = request?.ProjectionDefinitionId ?? string.Empty, MemberPersonIds = item.MemberPersonIds.ToArray(), InternalEdgeCount = item.EdgeCount, Density = item.Density, Truncated = item.Truncated, Algorithm = "threshold-connected-components" })
                .ToArray();
        }

        public SocialGroupMetricsResult CalculateGroupMetrics(string groupId, SocialGraphQueryRequest request)
        {
            string[] activeMembers = QueryMembers(groupId, activeOnly: true).Select(item => item.PersonId).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] historicalMembers = QueryMembers(groupId, activeOnly: false).Select(item => item.PersonId).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            SocialGraphSnapshot graph = BuildGraph(request);
            int internalEdges = graph.Edges.Count(edge => activeMembers.Contains(edge.SourcePersonId) && activeMembers.Contains(edge.DestinationPersonId));
            int possible = activeMembers.Length * Math.Max(0, activeMembers.Length - 1);
            string[] isolated = activeMembers.Where(person => QueryNeighbors(person, request).All(neighbor => !activeMembers.Contains(neighbor.PersonId))).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] bridges = activeMembers.Where(person => QueryNeighbors(person, request).Any(neighbor => !activeMembers.Contains(neighbor.PersonId))).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            return new SocialGroupMetricsResult
            {
                GroupId = groupId ?? string.Empty,
                ActiveMemberCount = activeMembers.Length,
                HistoricalMemberCount = historicalMembers.Length,
                InternalDensity = possible == 0 ? 0f : (float)internalEdges / possible,
                PositiveCohesion = graph.Edges.Count(edge => activeMembers.Contains(edge.SourcePersonId) && activeMembers.Contains(edge.DestinationPersonId) && edge.Valence == SocialGraphValence.Positive),
                HostilityConflict = graph.Edges.Count(edge => activeMembers.Contains(edge.SourcePersonId) && activeMembers.Contains(edge.DestinationPersonId) && edge.Valence == SocialGraphValence.Negative),
                IsolatedMembers = isolated,
                BridgeMembers = bridges,
                MutatedGroup = false
            };
        }

        public IReadOnlyList<SocialGroupSnapshot> QueryGroups(bool activeOnly = false) => groupsById.Values.Where(item => !activeOnly || item.lifecycle == InformalSocialGroupLifecycleStatus.Active).OrderBy(item => item.groupId, StringComparer.Ordinal).Select(item => new SocialGroupSnapshot(item)).ToArray();
        public SocialGroupSnapshot Snapshot(string groupId) => !string.IsNullOrWhiteSpace(groupId) && groupsById.TryGetValue(groupId, out SocialGroupRecordData data) ? new SocialGroupSnapshot(data) : null;
        public SocialGroupMembershipSnapshot SnapshotMembership(string membershipId) => !string.IsNullOrWhiteSpace(membershipId) && membershipsById.TryGetValue(membershipId, out SocialGroupMembershipRecordData data) ? new SocialGroupMembershipSnapshot(data) : null;
        public IReadOnlyList<SocialGroupSnapshot> QueryGroupsByPerson(string personId, bool activeOnly = true) => QueryMembershipsByPerson(personId, activeOnly).Select(item => Snapshot(item.GroupId)).Where(item => item != null).OrderBy(item => item.GroupId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<SocialGroupMembershipSnapshot> QueryMembershipsByPerson(string personId, bool activeOnly = true) => membershipsById.Values.Where(item => string.Equals(item.personId, personId, StringComparison.Ordinal) && (!activeOnly || item.status == SocialGroupMembershipStatus.Active)).OrderBy(item => item.groupId, StringComparer.Ordinal).ThenBy(item => item.membershipId, StringComparer.Ordinal).Select(item => new SocialGroupMembershipSnapshot(item)).ToArray();
        public IReadOnlyList<SocialGroupMembershipSnapshot> QueryMembers(string groupId, bool activeOnly = true) => membershipsById.Values.Where(item => string.Equals(item.groupId, groupId, StringComparison.Ordinal) && (!activeOnly || item.status == SocialGroupMembershipStatus.Active)).OrderBy(item => item.roleId, StringComparer.Ordinal).ThenBy(item => item.personId, StringComparer.Ordinal).ThenBy(item => item.membershipId, StringComparer.Ordinal).Select(item => new SocialGroupMembershipSnapshot(item)).ToArray();
        public IReadOnlyList<SocialGroupSnapshot> QueryGroupsByDefinition(string definitionId) => groupsById.Values.Where(item => string.Equals(item.groupDefinitionId, definitionId, StringComparison.Ordinal)).OrderBy(item => item.groupId, StringComparer.Ordinal).Select(item => new SocialGroupSnapshot(item)).ToArray();

        public SocialNetworkRuntimeSaveData CreateSaveData() => new SocialNetworkRuntimeSaveData { revision = Revision, groups = groupsById.Values.OrderBy(item => item.groupId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(), memberships = membershipsById.Values.OrderBy(item => item.membershipId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(), processedTransactions = processedTransactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList() };

        public SocialNetworkMutationResult RestoreFromSaveData(SocialNetworkRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failure))
            {
                return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.RestoreFailed, failure, revision: Revision);
            }
            RestoreInternal(saveData);
            IsDirty = !restoringState;
            registry = definitionRegistry ?? registry;
            knownPersonIds.Clear();
            foreach (string person in Clean(persons)) knownPersonIds.Add(person);
            return SocialNetworkMutationResult.Success(SocialNetworkOperationStatus.Succeeded, "Social networks restored.", string.Empty, null, null, Revision, Revision);
        }

        public static bool ValidateSaveData(SocialNetworkRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            if (saveData == null) { failure = "Social Network save data is missing."; return false; }
            if (saveData.schemaVersion != SocialNetworkRuntimeSaveData.CurrentSchemaVersion) { failure = $"Unsupported Social Network schema version {saveData.schemaVersion}."; return false; }
            HashSet<string> known = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialGroupRecordData group in saveData.groups ?? new List<SocialGroupRecordData>())
            {
                if (group == null || string.IsNullOrWhiteSpace(group.groupId)) { failure = "Social Network group record has no group ID."; return false; }
                if (!groupIds.Add(group.groupId)) { failure = $"Duplicate Social Network group ID '{group.groupId}'."; return false; }
                if (definitionRegistry == null || !definitionRegistry.TryGet(group.groupDefinitionId, out InformalSocialGroupDefinition _)) { failure = $"Social Network group '{group.groupId}' references missing group definition '{group.groupDefinitionId}'."; return false; }
            }
            HashSet<string> membershipIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activePairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialGroupMembershipRecordData membership in saveData.memberships ?? new List<SocialGroupMembershipRecordData>())
            {
                if (membership == null || string.IsNullOrWhiteSpace(membership.membershipId)) { failure = "Social Network membership has no membership ID."; return false; }
                if (!membershipIds.Add(membership.membershipId)) { failure = $"Duplicate Social Network membership ID '{membership.membershipId}'."; return false; }
                if (!groupIds.Contains(membership.groupId)) { failure = $"Social Network membership '{membership.membershipId}' references missing group '{membership.groupId}'."; return false; }
                if (!known.Contains(membership.personId)) { failure = $"Social Network membership '{membership.membershipId}' references unknown Person '{membership.personId}'."; return false; }
                SocialGroupRecordData group = saveData.groups.First(item => item.groupId == membership.groupId);
                if (definitionRegistry == null || !definitionRegistry.TryGet(group.groupDefinitionId, out InformalSocialGroupDefinition definition) || !definition.SupportsRole(membership.roleId)) { failure = $"Social Network membership '{membership.membershipId}' uses invalid role '{membership.roleId}'."; return false; }
                if (membership.status == SocialGroupMembershipStatus.Active && !activePairs.Add($"{membership.groupId}|{membership.personId}")) { failure = $"Social Network group '{membership.groupId}' has duplicate active membership for '{membership.personId}'."; return false; }
            }
            return true;
        }

        public void Clear()
        {
            groupsById.Clear();
            membershipsById.Clear();
            processedTransactions.Clear();
            graphCache.Clear();
            Revision = 0L;
            IsDirty = false;
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
            knownPersonIds.Clear();
            registry = null;
            relationships = null;
            attitudes = null;
            reputation = null;
            rumors = null;
            interactions = null;
            norms = null;
        }

        private SocialNetworkMutationResult CreateGroup(SocialGroupMutationRequest request, long before)
        {
            if (!TryGetGroupDefinition(request.GroupDefinitionId, out InformalSocialGroupDefinition definition, out SocialNetworkMutationResult failure, request, before)) return failure;
            string groupId = request.GroupId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(groupId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRequest, "Group ID is required.", request.TransactionId, before);
            if (groupsById.ContainsKey(groupId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.DuplicateGroupId, $"Group '{groupId}' already exists.", request.TransactionId, before);
            SocialGroupRecordData data = new SocialGroupRecordData { groupId = groupId, groupDefinitionId = definition.Id, displayName = request.DisplayName ?? groupId, audienceId = request.AudienceId ?? definition.AssociatedAudienceId, sourceCandidateId = request.SourceCandidateId ?? string.Empty, sourceProjectionDefinitionId = request.SourceProjectionDefinitionId ?? string.Empty, createdWorldTime = request.WorldTime, tags = SocialGroupRecordData.Clean(request.Tags), revision = Revision + (request.Preview ? 0L : 1L) };
            if (!request.Preview) groupsById[groupId] = data;
            return SocialNetworkMutationResult.Success(request.Preview ? SocialNetworkOperationStatus.Preview : SocialNetworkOperationStatus.Succeeded, "Social group created.", request.TransactionId, new SocialGroupSnapshot(data), null, before, before, preview: request.Preview);
        }

        private SocialNetworkMutationResult AddMembership(SocialGroupMutationRequest request, long before)
        {
            if (!TryGetGroup(request.GroupId, out SocialGroupRecordData group, out SocialNetworkMutationResult failure, request, before)) return failure;
            if (!TryGetGroupDefinition(group.groupDefinitionId, out InformalSocialGroupDefinition definition, out failure, request, before)) return failure;
            if (group.lifecycle == InformalSocialGroupLifecycleStatus.Dissolved) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.GroupDissolved, $"Group '{group.groupId}' is dissolved.", request.TransactionId, before);
            string membershipId = request.MembershipId?.Trim() ?? string.Empty;
            string personId = request.PersonId?.Trim() ?? string.Empty;
            string roleId = request.RoleId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(membershipId) || string.IsNullOrWhiteSpace(personId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRequest, "Membership ID and Person ID are required.", request.TransactionId, before);
            if (membershipsById.ContainsKey(membershipId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.DuplicateMembershipId, $"Membership '{membershipId}' already exists.", request.TransactionId, before);
            if (!knownPersonIds.Contains(personId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.UnknownPerson, $"Unknown Person '{personId}'.", request.TransactionId, before);
            if (!definition.SupportsRole(roleId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRole, $"Role '{roleId}' is not supported by group definition '{definition.Id}'.", request.TransactionId, before);
            if (membershipsById.Values.Any(item => item.status == SocialGroupMembershipStatus.Active && item.groupId == group.groupId && item.personId == personId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.DuplicateActiveMembership, $"Person '{personId}' is already active in group '{group.groupId}'.", request.TransactionId, before);
            if (definition.IsLeaderRole(roleId) && !definition.AllowsMultipleLeaders && membershipsById.Values.Any(item => item.status == SocialGroupMembershipStatus.Active && item.groupId == group.groupId && definition.IsLeaderRole(item.roleId))) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRole, $"Group '{group.groupId}' already has an active leader.", request.TransactionId, before);
            SocialGroupMembershipRecordData data = new SocialGroupMembershipRecordData { membershipId = membershipId, groupId = group.groupId, personId = personId, roleId = roleId, status = SocialGroupMembershipStatus.Active, joinedWorldTime = request.WorldTime, sourceRecordId = request.SourceRecordId ?? string.Empty, tags = SocialGroupRecordData.Clean(request.Tags), revision = Revision + (request.Preview ? 0L : 1L) };
            if (!request.Preview) membershipsById[membershipId] = data;
            return SocialNetworkMutationResult.Success(request.Preview ? SocialNetworkOperationStatus.Preview : SocialNetworkOperationStatus.Succeeded, "Social group membership added.", request.TransactionId, new SocialGroupSnapshot(group), new SocialGroupMembershipSnapshot(data), before, before, preview: request.Preview);
        }

        private SocialNetworkMutationResult ChangeMembershipRole(SocialGroupMutationRequest request, long before)
        {
            if (!membershipsById.TryGetValue(request.MembershipId ?? string.Empty, out SocialGroupMembershipRecordData membership)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.MissingGroup, $"Membership '{request.MembershipId}' is missing.", request.TransactionId, before);
            if (!TryGetGroup(membership.groupId, out SocialGroupRecordData group, out SocialNetworkMutationResult failure, request, before)) return failure;
            if (!TryGetGroupDefinition(group.groupDefinitionId, out InformalSocialGroupDefinition definition, out failure, request, before)) return failure;
            string roleId = request.RoleId?.Trim() ?? string.Empty;
            if (!definition.SupportsRole(roleId)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidRole, $"Role '{roleId}' is not supported.", request.TransactionId, before);
            SocialGroupMembershipRecordData changed = membership.Clone();
            changed.roleId = roleId;
            changed.revision = Revision + (request.Preview ? 0L : 1L);
            if (!request.Preview) membershipsById[changed.membershipId] = changed;
            return SocialNetworkMutationResult.Success(request.Preview ? SocialNetworkOperationStatus.Preview : SocialNetworkOperationStatus.Succeeded, "Social group membership role changed.", request.TransactionId, new SocialGroupSnapshot(group), new SocialGroupMembershipSnapshot(changed), before, before, preview: request.Preview);
        }

        private SocialNetworkMutationResult EndMembership(SocialGroupMutationRequest request, long before)
        {
            if (!membershipsById.TryGetValue(request.MembershipId ?? string.Empty, out SocialGroupMembershipRecordData membership)) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.MissingGroup, $"Membership '{request.MembershipId}' is missing.", request.TransactionId, before);
            SocialGroupMembershipRecordData ended = membership.Clone();
            ended.status = request.MutationKind == SocialGroupMutationKind.EndMembership ? SocialGroupMembershipStatus.Departed : ended.status;
            ended.endedWorldTime = request.WorldTime;
            ended.revision = Revision + (request.Preview ? 0L : 1L);
            if (!request.Preview) membershipsById[ended.membershipId] = ended;
            return SocialNetworkMutationResult.Success(request.Preview ? SocialNetworkOperationStatus.Preview : SocialNetworkOperationStatus.Succeeded, "Social group membership ended.", request.TransactionId, Snapshot(ended.groupId), new SocialGroupMembershipSnapshot(ended), before, before, preview: request.Preview);
        }

        private SocialNetworkMutationResult DissolveGroup(SocialGroupMutationRequest request, long before)
        {
            if (!TryGetGroup(request.GroupId, out SocialGroupRecordData group, out SocialNetworkMutationResult failure, request, before)) return failure;
            if (!TryGetGroupDefinition(group.groupDefinitionId, out InformalSocialGroupDefinition definition, out failure, request, before)) return failure;
            if (!definition.MayDissolve) return SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.InvalidLifecycle, $"Group '{group.groupId}' may not dissolve.", request.TransactionId, before);
            SocialGroupRecordData dissolved = group.Clone();
            dissolved.lifecycle = InformalSocialGroupLifecycleStatus.Dissolved;
            dissolved.dissolvedWorldTime = request.WorldTime;
            dissolved.revision = Revision + (request.Preview ? 0L : 1L);
            if (!request.Preview) groupsById[dissolved.groupId] = dissolved;
            return SocialNetworkMutationResult.Success(request.Preview ? SocialNetworkOperationStatus.Preview : SocialNetworkOperationStatus.Succeeded, "Social group dissolved.", request.TransactionId, new SocialGroupSnapshot(dissolved), null, before, before, preview: request.Preview);
        }

        private void AddRelationshipEdges(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, List<SocialGraphEdgeData> edges)
        {
            if (!definition.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.ObjectiveRelationship) || relationships == null) return;
            foreach (RelationshipRecordData record in relationships.CreateSaveData().records ?? new List<RelationshipRecordData>())
            {
                if (record.status != RelationshipLifecycleStatus.Active && !AllowsHistorical(definition, SocialGraphEdgeKind.ObjectiveRelationship)) continue;
                if (definition.RelationshipDefinitionFilters.Count > 0 && !definition.RelationshipDefinitionFilters.Contains(record.relationshipDefinitionId)) continue;
                string[] persons = record.participants?.Select(item => item.personId).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
                for (int i = 0; i < persons.Length; i++)
                for (int j = i + 1; j < persons.Length; j++)
                {
                    int weight = definition.WeightFor(SocialGraphEdgeKind.ObjectiveRelationship);
                    AddEdge(edges, definition, persons[i], persons[j], SocialGraphEdgeKind.ObjectiveRelationship, directed: false, new[] { record.recordId }, new[] { record.relationshipDefinitionId }, weight, weight, SocialGraphValence.Neutral, record.startWorldTime, "Formal relationship edge.");
                    AddEdge(edges, definition, persons[j], persons[i], SocialGraphEdgeKind.ObjectiveRelationship, directed: false, new[] { record.recordId }, new[] { record.relationshipDefinitionId }, weight, weight, SocialGraphValence.Neutral, record.startWorldTime, "Formal relationship symmetric edge.");
                }
            }
        }

        private void AddAttitudeEdges(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, List<SocialGraphEdgeData> edges)
        {
            bool directed = definition.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.DirectedAttitude);
            bool mutual = definition.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.MutualAttitude);
            if ((!directed && !mutual) || attitudes == null) return;
            InterpersonalAttitudeRecordData[] records = attitudes.CreateSaveData().records?.ToArray() ?? Array.Empty<InterpersonalAttitudeRecordData>();
            foreach (InterpersonalAttitudeRecordData record in records.OrderBy(item => item.observerPersonId, StringComparer.Ordinal).ThenBy(item => item.subjectPersonId, StringComparer.Ordinal))
            {
                foreach (AttitudeDimensionValueData dimension in record.dimensions ?? new List<AttitudeDimensionValueData>())
                {
                    if (definition.AttitudeDimensionFilters.Count > 0 && !definition.AttitudeDimensionFilters.Contains(dimension.dimensionId)) continue;
                    int value = dimension.hasBaseline ? dimension.baselineValue : 0;
                    value += dimension.contributions?.Sum(item => item.amount) ?? 0;
                    if (value == 0) continue;
                    int weight = Math.Min(100, Math.Abs(value));
                    SocialGraphValence valence = value > 0 ? SocialGraphValence.Positive : SocialGraphValence.Negative;
                    if (directed) AddEdge(edges, definition, record.observerPersonId, record.subjectPersonId, SocialGraphEdgeKind.DirectedAttitude, true, new[] { record.recordId }, new[] { dimension.dimensionId }, value, weight, valence, record.lastModifiedWorldTime, "Directional attitude edge.");
                    if (mutual)
                    {
                        InterpersonalAttitudeRecordData reciprocal = records.FirstOrDefault(item => item.observerPersonId == record.subjectPersonId && item.subjectPersonId == record.observerPersonId);
                        AttitudeDimensionValueData reciprocalDimension = reciprocal?.dimensions?.FirstOrDefault(item => item.dimensionId == dimension.dimensionId);
                        int reciprocalValue = reciprocalDimension == null ? 0 : (reciprocalDimension.hasBaseline ? reciprocalDimension.baselineValue : 0) + (reciprocalDimension.contributions?.Sum(item => item.amount) ?? 0);
                        if (reciprocalValue > 0 && value > 0 && string.CompareOrdinal(record.observerPersonId, record.subjectPersonId) < 0)
                        {
                            int mutualWeight = Math.Min(Math.Abs(value), Math.Abs(reciprocalValue));
                            AddEdge(edges, definition, record.observerPersonId, record.subjectPersonId, SocialGraphEdgeKind.MutualAttitude, false, new[] { record.recordId, reciprocal.recordId }, new[] { dimension.dimensionId }, value + reciprocalValue, mutualWeight, SocialGraphValence.Positive, Math.Max(record.lastModifiedWorldTime, reciprocal.lastModifiedWorldTime), "Mutual positive attitude edge.");
                            AddEdge(edges, definition, record.subjectPersonId, record.observerPersonId, SocialGraphEdgeKind.MutualAttitude, false, new[] { record.recordId, reciprocal.recordId }, new[] { dimension.dimensionId }, value + reciprocalValue, mutualWeight, SocialGraphValence.Positive, Math.Max(record.lastModifiedWorldTime, reciprocal.lastModifiedWorldTime), "Mutual positive attitude symmetric edge.");
                        }
                    }
                }
            }
        }

        private void AddInteractionEdges(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, List<SocialGraphEdgeData> edges)
        {
            if (!definition.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.RecentInteraction) || interactions == null) return;
            double now = request?.WorldTime ?? 0d;
            foreach (SocialInteractionRecordData record in interactions.CreateSaveData().records ?? new List<SocialInteractionRecordData>())
            {
                if (definition.TimeWindow >= 0d && now > 0d && record.worldTime < now - definition.TimeWindow) continue;
                if (definition.InteractionDefinitionFilters.Count > 0 && !definition.InteractionDefinitionFilters.Contains(record.interactionDefinitionId)) continue;
                if (string.IsNullOrWhiteSpace(record.initiatorPersonId) || string.IsNullOrWhiteSpace(record.targetPersonId)) continue;
                int weight = definition.WeightFor(SocialGraphEdgeKind.RecentInteraction);
                AddEdge(edges, definition, record.initiatorPersonId, record.targetPersonId, SocialGraphEdgeKind.RecentInteraction, true, new[] { record.interactionRecordId }, new[] { record.interactionDefinitionId }, 1, weight, InteractionValence(record), record.worldTime, "Recent social interaction edge.");
            }
        }

        private void AddRumorEdges(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, List<SocialGraphEdgeData> edges)
        {
            if (!definition.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.RumorTransmission) || rumors == null) return;
            foreach (RumorTransmissionRecordData record in rumors.CreateSaveData().transmissions ?? Array.Empty<RumorTransmissionRecordData>())
            {
                if (string.IsNullOrWhiteSpace(record.speakerPersonId) || string.IsNullOrWhiteSpace(record.listenerPersonId)) continue;
                int weight = definition.WeightFor(SocialGraphEdgeKind.RumorTransmission);
                AddEdge(edges, definition, record.speakerPersonId, record.listenerPersonId, SocialGraphEdgeKind.RumorTransmission, true, new[] { record.transmissionId }, new[] { record.channelId }, record.speakerConfidence, weight, SocialGraphValence.Unsigned, record.transmissionWorldTime, "Rumor transmission edge.");
            }
        }

        private void AddGroupEdges(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, List<SocialGraphEdgeData> edges)
        {
            if (!definition.IncludedEdgeKinds.Contains(SocialGraphEdgeKind.SharedGroupMembership)) return;
            foreach (SocialGroupRecordData group in groupsById.Values.Where(item => item.lifecycle == InformalSocialGroupLifecycleStatus.Active).OrderBy(item => item.groupId, StringComparer.Ordinal))
            {
                string[] members = QueryMembers(group.groupId, activeOnly: true).Select(item => item.PersonId).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < members.Length; i++)
                for (int j = i + 1; j < members.Length; j++)
                {
                    int weight = definition.WeightFor(SocialGraphEdgeKind.SharedGroupMembership);
                    AddEdge(edges, definition, members[i], members[j], SocialGraphEdgeKind.SharedGroupMembership, false, new[] { group.groupId }, new[] { group.groupDefinitionId }, members.Length, weight, SocialGraphValence.Neutral, group.createdWorldTime, "Shared informal group membership edge.");
                    AddEdge(edges, definition, members[j], members[i], SocialGraphEdgeKind.SharedGroupMembership, false, new[] { group.groupId }, new[] { group.groupDefinitionId }, members.Length, weight, SocialGraphValence.Neutral, group.createdWorldTime, "Shared informal group membership symmetric edge.");
                }
            }
        }

        private void AddEdge(List<SocialGraphEdgeData> edges, SocialGraphProjectionDefinition definition, string source, string destination, SocialGraphEdgeKind kind, bool directed, IEnumerable<string> records, IEnumerable<string> definitionIds, int rawValue, int weight, SocialGraphValence valence, double worldTime, string explanation)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination) || source == destination) return;
            int normalized = Math.Min(100, Math.Max(0, weight));
            if (normalized < definition.MinimumEdgeWeight) return;
            string edgeId = $"{definition.Id}.{kind}.{source}.{destination}.{string.Join(".", Clean(records))}";
            edges.Add(new SocialGraphEdgeData { edgeId = edgeId, sourcePersonId = source, destinationPersonId = destination, edgeKind = kind, directed = directed, sourceRecordIds = Clean(records), definitionOrDimensionIds = Clean(definitionIds), rawValue = rawValue, normalizedWeight = normalized, valence = valence, worldTime = worldTime, visibility = definition.Visibility, projectionDefinitionId = definition.Id, explanation = explanation });
        }

        private bool TryGetGroupDefinition(string definitionId, out InformalSocialGroupDefinition definition, out SocialNetworkMutationResult failure, SocialGroupMutationRequest request, long before)
        {
            definition = null;
            failure = null;
            if (registry == null)
            {
                failure = SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.MissingDefinitionRegistry, "Definition registry is missing.", request?.TransactionId, before);
                return false;
            }
            if (!registry.TryGet(definitionId ?? string.Empty, out definition))
            {
                failure = SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.MissingDefinition, $"Informal group definition '{definitionId}' is missing.", request?.TransactionId, before);
                return false;
            }
            return true;
        }

        private bool TryGetGroup(string groupId, out SocialGroupRecordData group, out SocialNetworkMutationResult failure, SocialGroupMutationRequest request, long before)
        {
            group = null;
            failure = null;
            if (!groupsById.TryGetValue(groupId ?? string.Empty, out group))
            {
                failure = SocialNetworkMutationResult.Failure(SocialNetworkOperationStatus.MissingGroup, $"Group '{groupId}' is missing.", request?.TransactionId, before);
                return false;
            }
            return true;
        }

        private SocialGraphProjectionDefinition ResolveProjection(string id)
        {
            if (registry != null && registry.TryGet(id ?? string.Empty, out SocialGraphProjectionDefinition definition)) return definition;
            return null;
        }

        private SocialGraphRevisionDependenciesData CaptureDependencies() => new SocialGraphRevisionDependenciesData { relationshipRevision = relationships?.Revision ?? 0L, attitudeRevision = attitudes?.Revision ?? 0L, reputationRevision = reputation?.Revision ?? 0L, rumorRevision = rumors?.Revision ?? 0L, interactionRevision = interactions?.Revision ?? 0L, normRevision = norms?.Revision ?? 0L, groupRevision = Revision };

        private void RestoreInternal(SocialNetworkRuntimeSaveData saveData)
        {
            groupsById.Clear();
            membershipsById.Clear();
            processedTransactions.Clear();
            foreach (SocialGroupRecordData group in saveData?.groups ?? new List<SocialGroupRecordData>()) groupsById[group.groupId] = group.Clone();
            foreach (SocialGroupMembershipRecordData membership in saveData?.memberships ?? new List<SocialGroupMembershipRecordData>()) membershipsById[membership.membershipId] = membership.Clone();
            foreach (SocialNetworkProcessedTransactionData processed in saveData?.processedTransactions ?? new List<SocialNetworkProcessedTransactionData>()) processedTransactions[processed.transactionId] = processed.Clone();
            Revision = saveData?.revision ?? 0L;
            graphCache.Clear();
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static IEnumerable<SocialGraphEdgeData> OrderedEdges(IEnumerable<SocialGraphEdgeData> edges) => edges.OrderBy(item => item.sourcePersonId, StringComparer.Ordinal).ThenBy(item => item.destinationPersonId, StringComparer.Ordinal).ThenBy(item => item.edgeKind).ThenBy(item => item.edgeId, StringComparer.Ordinal);
        private static string PairKey(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
        private static bool AllConnected(string[] members, HashSet<string> pairs) => members.SelectMany((first, i) => members.Skip(i + 1).Select(second => PairKey(first, second))).All(pairs.Contains);
        private static bool AllowsHistorical(SocialGraphProjectionDefinition definition, SocialGraphEdgeKind kind) => definition.EdgeWeights.Any(item => item.edgeKind == kind && item.includeHistorical);
        private static SocialGraphValence InteractionValence(SocialInteractionRecordData record) => record.outcome == SocialInteractionOutcome.Failure || record.outcome == SocialInteractionOutcome.Refused || record.outcome == SocialInteractionOutcome.Blocked ? SocialGraphValence.Negative : SocialGraphValence.Unsigned;
        private static List<SocialGraphEdgeData> ApplyRequestFilters(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, IEnumerable<SocialGraphEdgeData> edges)
        {
            HashSet<SocialGraphEdgeKind> requestedKinds = new HashSet<SocialGraphEdgeKind>(request?.EdgeKinds ?? Array.Empty<SocialGraphEdgeKind>());
            int minimumWeight = Math.Max(definition.MinimumEdgeWeight, request?.MinimumWeight ?? 0);
            return (edges ?? Array.Empty<SocialGraphEdgeData>())
                .Where(edge => requestedKinds.Count == 0 || requestedKinds.Contains(edge.edgeKind))
                .Where(edge => edge.normalizedWeight >= minimumWeight)
                .OrderBy(edge => edge.sourcePersonId, StringComparer.Ordinal)
                .ThenBy(edge => edge.destinationPersonId, StringComparer.Ordinal)
                .ThenBy(edge => edge.edgeKind)
                .ThenBy(edge => edge.edgeId, StringComparer.Ordinal)
                .ToList();
        }

        private static string CacheKey(SocialGraphProjectionDefinition definition, SocialGraphQueryRequest request, SocialGraphRevisionDependenciesData dependencies)
        {
            string kinds = string.Join(",", (request?.EdgeKinds ?? Array.Empty<SocialGraphEdgeKind>()).Distinct().OrderBy(item => item).Select(item => item.ToString()));
            int minimumWeight = Math.Max(definition.MinimumEdgeWeight, request?.MinimumWeight ?? 0);
            return $"{definition.Id}|{request?.WorldTime ?? 0d}|{request?.Visibility ?? definition.Visibility}|{request?.MaxDepth ?? 0}|{request?.MaxVisitedNodes ?? 0}|{minimumWeight}|{kinds}|{dependencies.relationshipRevision}.{dependencies.attitudeRevision}.{dependencies.reputationRevision}.{dependencies.rumorRevision}.{dependencies.interactionRevision}.{dependencies.normRevision}.{dependencies.groupRevision}";
        }
        private void StampResult(SocialNetworkMutationResult result, long revision) { if (result?.Group?.GroupId != null && groupsById.TryGetValue(result.Group.GroupId, out SocialGroupRecordData group)) group.revision = revision; if (result?.Membership?.MembershipId != null && membershipsById.TryGetValue(result.Membership.MembershipId, out SocialGroupMembershipRecordData membership)) membership.revision = revision; }
    }
}
