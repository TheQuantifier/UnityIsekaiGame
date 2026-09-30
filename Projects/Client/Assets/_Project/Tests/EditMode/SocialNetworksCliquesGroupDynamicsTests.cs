#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class SocialNetworksCliquesGroupDynamicsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        private static readonly string[] KnownPersons =
        {
            PersistenceService.LocalPlayerId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.mentor",
            "person.prototype.student",
            "person.prototype.listener"
        };

        [Test]
        public void PrototypeNetworkDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, out SocialGraphProjectionDefinition projection), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId, out InformalSocialGroupDefinition group), Is.True);
            Assert.That(projection.IncludedEdgeKinds, Does.Contain(SocialGraphEdgeKind.SharedGroupMembership));
            Assert.That(group.RequiresLeader, Is.True);
            Assert.That(group.SupportsRole(PrototypeSocialNetworkDefinitionFactory.LeaderRoleId), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (object definition in PrototypeSocialNetworkDefinitionFactory.CreateDefinitions())
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void GraphProjectionPreservesEdgeSemanticsAndImmutableSnapshots()
        {
            using TestFixture fixture = CreateSeededFixture();
            SocialGraphQueryRequest request = Query(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, 100d);

            SocialGraphSnapshot first = fixture.Networks.BuildGraph(request);
            SocialGraphSnapshot second = fixture.Networks.BuildGraph(request);
            SocialGraphNodeSnapshot mutableNode = first.Nodes.First(item => item.NodeId == "person.prototype.friend");
            mutableNode.Data.nodeId = "person.mutated";
            SocialGraphEdgeSnapshot mutableEdge = first.Edges.First();
            mutableEdge.Data.sourcePersonId = "person.mutated";
            SocialGraphSnapshot afterMutationAttempt = fixture.Networks.BuildGraph(request);
            string edgeDiagnostics = string.Join(", ", first.Edges.Select(edge => $"{edge.EdgeKind}:{edge.SourcePersonId}->{edge.DestinationPersonId}:{edge.Data.definitionOrDimensionIds.FirstOrDefault()}"));

            Assert.That(ReferenceEquals(first, second), Is.True, "Equivalent requests should reuse the deterministic cache until dependencies change.");
            Assert.That(first.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.ObjectiveRelationship), Is.True, edgeDiagnostics);
            Assert.That(first.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.DirectedAttitude), Is.True, edgeDiagnostics);
            Assert.That(first.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.RecentInteraction), Is.True, edgeDiagnostics);
            Assert.That(first.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.RumorTransmission), Is.True, edgeDiagnostics);
            Assert.That(first.Edges.Any(edge => edge.EdgeKind == SocialGraphEdgeKind.SharedGroupMembership), Is.True, edgeDiagnostics);
            Assert.That(afterMutationAttempt.Nodes.Any(item => item.NodeId == "person.mutated"), Is.False);
            Assert.That(afterMutationAttempt.Edges.Any(item => item.SourcePersonId == "person.mutated"), Is.False);
        }

        [Test]
        public void NeighborTraversalMetricsCliquesAndCommunitiesAreBoundedAndNonMutating()
        {
            using TestFixture fixture = CreateSeededFixture();
            long before = fixture.Networks.Revision;
            SocialGraphQueryRequest request = Query(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, 100d);

            var neighbors = fixture.Networks.QueryNeighbors(PersistenceService.LocalPlayerId, request);
            var mutual = fixture.Networks.QueryMutualConnections(PersistenceService.LocalPlayerId, "person.prototype.student", request);
            SocialGraphPathResult path = fixture.Networks.FindShortestPath(PersistenceService.LocalPlayerId, "person.prototype.student", request);
            SocialGraphMetricsResult metrics = fixture.Networks.CalculatePersonMetrics(PersistenceService.LocalPlayerId, request);
            var components = fixture.Networks.FindConnectedComponents(request);
            var cliques = fixture.Networks.FindCliqueCandidates(Query(PrototypeSocialNetworkDefinitionFactory.MutualTrustProjectionId, 100d));
            var communities = fixture.Networks.FindCommunityCandidates(request);
            string cliqueDiagnostics = string.Join(", ", fixture.Networks.BuildGraph(Query(PrototypeSocialNetworkDefinitionFactory.MutualTrustProjectionId, 100d)).Edges.Select(edge => $"{edge.SourcePersonId}->{edge.DestinationPersonId}:{edge.Weight}"));

            Assert.That(neighbors.Count, Is.GreaterThan(0));
            Assert.That(mutual.Any(item => item.MutualPersonId == "person.prototype.friend"), Is.True);
            Assert.That(path.Connected, Is.True, path.Diagnostics);
            Assert.That(path.PersonPath.Length, Is.LessThanOrEqualTo(request.MaxDepth + 1));
            Assert.That(metrics.Degree, Is.GreaterThan(0));
            Assert.That(components.Count, Is.GreaterThan(0));
            Assert.That(cliques.Any(item => item.MemberPersonIds.Contains(PersistenceService.LocalPlayerId)), Is.True, cliqueDiagnostics);
            Assert.That(communities.Count, Is.GreaterThan(0));
            Assert.That(fixture.Networks.Revision, Is.EqualTo(before), "Projection analysis must not create social groups or mutate source state.");
        }

        [Test]
        public void GroupLifecyclePreviewDuplicateRolesAndMetricsAreStable()
        {
            using TestFixture fixture = CreateFixture();
            SocialGroupMutationRequest create = GroupRequest("network.tx.preview", SocialGroupMutationKind.CreateGroup, "group.prototype.party", preview: true);
            SocialNetworkMutationResult preview = fixture.Networks.Mutate(create);
            SocialNetworkMutationResult execute = fixture.Networks.Mutate(GroupRequest("network.tx.create", SocialGroupMutationKind.CreateGroup, "group.prototype.party"));
            SocialNetworkMutationResult duplicate = fixture.Networks.Mutate(GroupRequest("network.tx.create", SocialGroupMutationKind.CreateGroup, "group.prototype.party"));
            SocialNetworkMutationResult leader = fixture.Networks.Mutate(MembershipRequest("network.tx.leader", "group.prototype.party", PersistenceService.LocalPlayerId, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId));
            SocialNetworkMutationResult member = fixture.Networks.Mutate(MembershipRequest("network.tx.friend", "group.prototype.party", "person.prototype.friend", PrototypeSocialNetworkDefinitionFactory.CompanionRoleId));
            SocialNetworkMutationResult role = fixture.Networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = "network.tx.friend.role",
                MutationKind = SocialGroupMutationKind.ChangeMembershipRole,
                MembershipId = "membership.group.prototype.party.person.prototype.friend",
                RoleId = PrototypeSocialNetworkDefinitionFactory.CompanionRoleId,
                WorldTime = 3d
            });
            SocialGroupMetricsResult metrics = fixture.Networks.CalculateGroupMetrics("group.prototype.party", Query(PrototypeSocialNetworkDefinitionFactory.CompositeProjectionId, 100d));

            Assert.That(preview.Status, Is.EqualTo(SocialNetworkOperationStatus.Preview), preview.Message);
            Assert.That(fixture.Networks.GroupCount, Is.EqualTo(1), "Only the executed group should be stored.");
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Status, Is.EqualTo(SocialNetworkOperationStatus.Duplicate), duplicate.Message);
            Assert.That(leader.Succeeded, Is.True, leader.Message);
            Assert.That(member.Succeeded, Is.True, member.Message);
            Assert.That(role.Succeeded, Is.True, role.Message);
            Assert.That(metrics.ActiveMemberCount, Is.EqualTo(2));
            Assert.That(metrics.MutatedGroup, Is.False);
        }

        [Test]
        public void PersistenceRoundTripAndInvalidRestoreRejectWithoutMutation()
        {
            using TestFixture fixture = CreateSeededFixture();
            SocialNetworkPersistenceParticipant participant = new SocialNetworkPersistenceParticipant(fixture.Networks, () => fixture.Registry, () => KnownPersons);

            PersistenceParticipantSaveResult saved = participant.CapturePayload();
            SocialNetworkRuntimeSaveData saveData = JsonUtility.FromJson<SocialNetworkRuntimeSaveData>(saved.PayloadJson);
            SocialNetworkRuntime restored = new SocialNetworkRuntime();
            restored.Configure(fixture.Registry, KnownPersons, fixture.Relationships, fixture.Attitudes, fixture.Reputation, fixture.Rumors, fixture.Interactions, fixture.Norms);
            SocialNetworkMutationResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);
            SocialNetworkRuntimeSaveData corrupt = saveData.Clone();
            corrupt.memberships[0].personId = "person.prototype.unknown";
            int beforeGroups = fixture.Networks.GroupCount;
            int beforeMemberships = fixture.Networks.MembershipCount;
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialNetworkPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(saved.Succeeded, Is.True, saved.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.GroupCount, Is.EqualTo(fixture.Networks.GroupCount));
            Assert.That(restored.MembershipCount, Is.EqualTo(fixture.Networks.MembershipCount));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Networks.GroupCount, Is.EqualTo(beforeGroups));
            Assert.That(fixture.Networks.MembershipCount, Is.EqualTo(beforeMemberships));
        }

        private static TestFixture CreateSeededFixture()
        {
            TestFixture fixture = CreateFixture();
            RelationshipOperationResult friend = fixture.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.player.friend",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                transactionId = "relationship.tx.player.friend",
                startWorldTime = 1d
            });
            RelationshipOperationResult mentor = fixture.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.friend.student",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                firstPersonId = "person.prototype.friend",
                firstRoleId = "mentor",
                secondPersonId = "person.prototype.student",
                secondRoleId = "student",
                transactionId = "relationship.tx.friend.student",
                startWorldTime = 2d
            });
            Assert.That(friend.Succeeded, Is.True, friend.Message);
            Assert.That(mentor.Succeeded, Is.True, mentor.Message);

            MutateAttitude(fixture.Attitudes, "attitude.tx.player.friend", PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId, 70);
            MutateAttitude(fixture.Attitudes, "attitude.tx.friend.player", "person.prototype.friend", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.TrustId, 65);
            MutateAttitude(fixture.Attitudes, "attitude.tx.friend.student", "person.prototype.friend", "person.prototype.student", PrototypeAttitudeDefinitionFactory.TrustId, 62);
            MutateAttitude(fixture.Attitudes, "attitude.tx.student.friend", "person.prototype.student", "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId, 58);
            MutateAttitude(fixture.Attitudes, "attitude.tx.player.student", PersistenceService.LocalPlayerId, "person.prototype.student", PrototypeAttitudeDefinitionFactory.TrustId, 55);
            MutateAttitude(fixture.Attitudes, "attitude.tx.student.player", "person.prototype.student", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.TrustId, 52);
            MutateAttitude(fixture.Attitudes, "attitude.tx.player.rival", PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId, -40);

            SocialInteractionResult interaction = fixture.Interactions.Execute(new SocialInteractionRequest
            {
                TransactionId = "interaction.tx.player.friend",
                InteractionDefinitionId = PrototypeSocialInteractionDefinitionFactory.GreetId,
                InitiatorPersonId = PersistenceService.LocalPlayerId,
                TargetPersonId = "person.prototype.friend",
                PlaceId = "place.prototype.test-lab",
                Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Person, subjectId = "person.prototype.friend" },
                Channel = SocialInteractionCommunicationChannel.Conversation,
                WorldTime = 30d,
                DeterministicSeed = "social-network-tests"
            });
            Assert.That(interaction.Succeeded, Is.True, interaction.Message);

            RumorOperationResult rumor = fixture.Rumors.CreateRumor(new RumorCreateRequest
            {
                TransactionId = "rumor.tx.create",
                RumorId = "rumor.test.network",
                DefinitionId = PrototypeRumorDefinitionFactory.PublicNewsRumorId,
                Claim = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                    subjectType = KnowledgeSubjectType.Event,
                    subjectId = "event.test.network",
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true
                },
                OriginatorPersonId = PersistenceService.LocalPlayerId,
                OriginCategory = RumorOriginCategory.FirsthandObservation,
                Confidence = 700,
                Salience = 600,
                Memorability = 600,
                WorldTime = 32d
            });
            Assert.That(rumor.Succeeded, Is.True, rumor.Message);
            RumorOperationResult transmission = fixture.Rumors.Transmit(new RumorTransmissionRequest
            {
                TransactionId = "rumor.tx.transmit",
                RumorVersionId = "rumor.test.network",
                SpeakerPersonId = PersistenceService.LocalPlayerId,
                ListenerPersonId = "person.prototype.friend",
                ChannelId = PrototypeRumorDefinitionFactory.ConversationChannelId,
                TransmissionId = "rumor-transmission.test.network",
                WorldTime = 34d,
                DeterministicSeed = "social-network-tests"
            });
            Assert.That(transmission.Succeeded, Is.True, transmission.Message);

            Assert.That(fixture.Networks.Mutate(GroupRequest("network.tx.seed.group", SocialGroupMutationKind.CreateGroup, "group.prototype.party")).Succeeded, Is.True);
            Assert.That(fixture.Networks.Mutate(MembershipRequest("network.tx.seed.leader", "group.prototype.party", PersistenceService.LocalPlayerId, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId)).Succeeded, Is.True);
            Assert.That(fixture.Networks.Mutate(MembershipRequest("network.tx.seed.friend", "group.prototype.party", "person.prototype.friend", PrototypeSocialNetworkDefinitionFactory.CompanionRoleId)).Succeeded, Is.True);
            Assert.That(fixture.Networks.Mutate(MembershipRequest("network.tx.seed.student", "group.prototype.party", "person.prototype.student", PrototypeSocialNetworkDefinitionFactory.CompanionRoleId)).Succeeded, Is.True);
            return fixture;
        }

        private static void MutateAttitude(InterpersonalAttitudeRuntime runtime, string transactionId, string observer, string subject, string dimension, int value)
        {
            AttitudeMutationResult result = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = transactionId,
                observerPersonId = observer,
                subjectPersonId = subject,
                dimensionId = dimension,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = value,
                worldTime = 4d
            });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static SocialGroupMutationRequest GroupRequest(string transactionId, SocialGroupMutationKind kind, string groupId, bool preview = false)
        {
            return new SocialGroupMutationRequest
            {
                TransactionId = transactionId,
                MutationKind = kind,
                GroupId = groupId,
                GroupDefinitionId = PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId,
                DisplayName = "Prototype Test Party",
                WorldTime = 1d,
                Preview = preview,
                Tags = new[] { "test-lab", "prototype" }
            };
        }

        private static SocialGroupMutationRequest MembershipRequest(string transactionId, string groupId, string personId, string roleId)
        {
            return new SocialGroupMutationRequest
            {
                TransactionId = transactionId,
                MutationKind = SocialGroupMutationKind.AddMembership,
                GroupId = groupId,
                MembershipId = $"membership.{groupId}.{personId}",
                PersonId = personId,
                RoleId = roleId,
                WorldTime = 2d,
                Tags = new[] { "test-lab" }
            };
        }

        private static SocialGraphQueryRequest Query(string projectionId, double worldTime)
        {
            return new SocialGraphQueryRequest
            {
                ProjectionDefinitionId = projectionId,
                WorldTime = worldTime,
                MaxDepth = 4,
                MaxVisitedNodes = 16,
                MinimumWeight = 1,
                Visibility = SocialGraphVisibility.Authoritative
            };
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            GameObject owner = new GameObject("Social network test knowledge runtime");
            PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            RumorRuntime rumors = new RumorRuntime();
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            SocialNormRuntime norms = new SocialNormRuntime();
            SocialNetworkRuntime networks = new SocialNetworkRuntime();

            history.Configure(registry, PersistenceService.LocalWorldId, KnownPersons, Array.Empty<string>());
            knowledge.Configure(registry, PersistenceService.LocalPlayerId);
            memory.Configure(PersistenceService.LocalPlayerId, registry, history, KnownPersons);
            relationships.Configure(registry, KnownPersons);
            attitudes.Configure(registry, KnownPersons);
            reputation.Configure(registry, KnownPersons);
            rumors.Configure(
                registry,
                KnownPersons,
                personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? knowledge : null,
                personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? memory : null);
            interactions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors);
            norms.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions);
            networks.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions, norms);
            return new TestFixture(registry, owner, relationships, attitudes, reputation, rumors, interactions, norms, networks);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                    PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                        PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                            PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                                PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                    PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry())))))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, GameObject owner, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions, SocialNormRuntime norms, SocialNetworkRuntime networks)
            {
                Registry = registry;
                Owner = owner;
                Relationships = relationships;
                Attitudes = attitudes;
                Reputation = reputation;
                Rumors = rumors;
                Interactions = interactions;
                Norms = norms;
                Networks = networks;
            }

            public DefinitionRegistry Registry { get; }
            public GameObject Owner { get; }
            public RelationshipRuntime Relationships { get; }
            public InterpersonalAttitudeRuntime Attitudes { get; }
            public ReputationRuntime Reputation { get; }
            public RumorRuntime Rumors { get; }
            public SocialInteractionRuntime Interactions { get; }
            public SocialNormRuntime Norms { get; }
            public SocialNetworkRuntime Networks { get; }

            public void Dispose()
            {
                Networks.Dispose();
                Norms.Dispose();
                Interactions.Dispose();
                Rumors.Dispose();
                UnityEngine.Object.DestroyImmediate(Owner);
            }
        }
    }
}
#endif
