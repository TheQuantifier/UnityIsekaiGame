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
    public static class PrototypeSocialNetworkDefinitionFactory
    {
        public const string RelationshipProjectionId = "social-graph.projection.prototype.relationships";
        public const string MutualTrustProjectionId = "social-graph.projection.prototype.mutual-trust";
        public const string CompositeProjectionId = "social-graph.projection.prototype.composite-social";
        public const string RumorReachProjectionId = "social-graph.projection.prototype.rumor-reach";
        public const string FriendCircleGroupId = "social-group.prototype.friend-circle";
        public const string AdventuringPartyGroupId = "social-group.prototype.adventuring-party";
        public const string LeaderRoleId = "leader";
        public const string MemberRoleId = "member";
        public const string CompanionRoleId = "companion";

        public static IReadOnlyList<object> CreateDefinitions()
        {
            return new object[]
            {
                RelationshipProjection(),
                MutualTrustProjection(),
                CompositeProjection(),
                RumorReachProjection(),
                FriendCircleGroup(),
                AdventuringPartyGroup()
            };
        }

        public static DefinitionRegistry AddMissingPrototypeSocialNetworkDefinitions(DefinitionRegistry baseRegistry)
        {
            IGameDefinition[] existing = baseRegistry?.DefinitionsById?.Values?.OfType<IGameDefinition>().ToArray() ?? Array.Empty<IGameDefinition>();
            IGameDefinition[] additions = CreateDefinitions()
                .OfType<IGameDefinition>()
                .Where(definition => baseRegistry == null || !baseRegistry.Contains(definition.Id))
                .ToArray();
            return new DefinitionRegistry(existing.Concat(additions));
        }

        private static SocialGraphProjectionDefinition RelationshipProjection()
        {
            SocialGraphProjectionDefinition definition = CreateProjection();
            definition.DevelopmentConfigure(
                RelationshipProjectionId,
                "Prototype Relationship Network",
                new[] { SocialGraphEdgeKind.ObjectiveRelationship, SocialGraphEdgeKind.SharedGroupMembership },
                SocialGraphDirectionPolicy.ExplicitRelationshipSymmetry,
                SocialGraphWeightPolicy.RelationshipCategory,
                minimumWeight: 1,
                maxNodes: 96,
                maxEdges: 384,
                maxDepth: 4,
                weights: new[] { Weight(SocialGraphEdgeKind.ObjectiveRelationship, 60), Weight(SocialGraphEdgeKind.SharedGroupMembership, 45) },
                text: "Stable relationship and group-membership graph for local social neighborhood analysis.",
                tagIds: new[] { "prototype", "relationship" });
            return definition;
        }

        private static SocialGraphProjectionDefinition MutualTrustProjection()
        {
            SocialGraphProjectionDefinition definition = CreateProjection();
            definition.DevelopmentConfigure(
                MutualTrustProjectionId,
                "Prototype Mutual Trust Network",
                new[] { SocialGraphEdgeKind.MutualAttitude },
                SocialGraphDirectionPolicy.RequireMutual,
                SocialGraphWeightPolicy.AttitudeMagnitude,
                minimumWeight: 10,
                maxNodes: 64,
                maxEdges: 256,
                maxDepth: 3,
                attitudeFilters: new[] { PrototypeAttitudeDefinitionFactory.TrustId, PrototypeAttitudeDefinitionFactory.AffectionId },
                weights: new[] { Weight(SocialGraphEdgeKind.MutualAttitude, 70) },
                text: "Mutual positive-attitude projection used for clique candidates.",
                tagIds: new[] { "prototype", "mutual", "trust" });
            return definition;
        }

        private static SocialGraphProjectionDefinition CompositeProjection()
        {
            SocialGraphProjectionDefinition definition = CreateProjection();
            definition.DevelopmentConfigure(
                CompositeProjectionId,
                "Prototype Composite Social Network",
                new[]
                {
                    SocialGraphEdgeKind.ObjectiveRelationship,
                    SocialGraphEdgeKind.DirectedAttitude,
                    SocialGraphEdgeKind.RecentInteraction,
                    SocialGraphEdgeKind.RumorTransmission,
                    SocialGraphEdgeKind.SharedGroupMembership
                },
                SocialGraphDirectionPolicy.PreserveDirection,
                SocialGraphWeightPolicy.Composite,
                minimumWeight: 1,
                maxNodes: 128,
                maxEdges: 512,
                maxDepth: 5,
                maxAnalysisNodes: 96,
                maxClique: 5,
                maxResults: 24,
                window: 90d,
                attitudeFilters: new[] { PrototypeAttitudeDefinitionFactory.TrustId, PrototypeAttitudeDefinitionFactory.AffectionId, PrototypeAttitudeDefinitionFactory.HostilityId, PrototypeAttitudeDefinitionFactory.RespectId },
                interactionFilters: new[] { PrototypeSocialInteractionDefinitionFactory.GreetId, PrototypeSocialInteractionDefinitionFactory.ComplimentId, PrototypeSocialInteractionDefinitionFactory.InsultId, PrototypeSocialInteractionDefinitionFactory.ShareInformationId },
                weights: new[]
                {
                    Weight(SocialGraphEdgeKind.ObjectiveRelationship, 55),
                    Weight(SocialGraphEdgeKind.DirectedAttitude, 50),
                    Weight(SocialGraphEdgeKind.RecentInteraction, 30),
                    Weight(SocialGraphEdgeKind.RumorTransmission, 25),
                    Weight(SocialGraphEdgeKind.SharedGroupMembership, 65)
                },
                text: "Composite graph that keeps source semantics while supporting bounded traversal and diagnostics.",
                tagIds: new[] { "prototype", "composite" });
            return definition;
        }

        private static SocialGraphProjectionDefinition RumorReachProjection()
        {
            SocialGraphProjectionDefinition definition = CreateProjection();
            definition.DevelopmentConfigure(
                RumorReachProjectionId,
                "Prototype Rumor Reach Network",
                new[] { SocialGraphEdgeKind.RumorTransmission, SocialGraphEdgeKind.RecentInteraction },
                SocialGraphDirectionPolicy.PreserveDirection,
                SocialGraphWeightPolicy.RumorTransmissionCount,
                minimumWeight: 1,
                maxNodes: 96,
                maxEdges: 384,
                maxDepth: 4,
                weights: new[] { Weight(SocialGraphEdgeKind.RumorTransmission, 45), Weight(SocialGraphEdgeKind.RecentInteraction, 20) },
                text: "Bounded information-flow projection for rumor propagation candidate queries.",
                tagIds: new[] { "prototype", "rumor" });
            return definition;
        }

        private static InformalSocialGroupDefinition FriendCircleGroup()
        {
            InformalSocialGroupDefinition definition = CreateGroup();
            definition.DevelopmentConfigure(
                FriendCircleGroupId,
                "Prototype Friend Circle",
                InformalSocialGroupCategory.FriendCircle,
                2,
                12,
                leaderRequired: false,
                multipleLeaders: true,
                InformalSocialGroupVisibility.MembersOnly,
                Roles(MemberRoleId, "Member", false),
                projectionIds: new[] { MutualTrustProjectionId },
                text: "A non-institutional circle of friends inferred or explicitly created from recurring positive contact.",
                tagIds: new[] { "prototype", "friend-circle" });
            return definition;
        }

        private static InformalSocialGroupDefinition AdventuringPartyGroup()
        {
            InformalSocialGroupDefinition definition = CreateGroup();
            definition.DevelopmentConfigure(
                AdventuringPartyGroupId,
                "Prototype Adventuring Party",
                InformalSocialGroupCategory.AdventuringParty,
                2,
                8,
                leaderRequired: true,
                multipleLeaders: false,
                InformalSocialGroupVisibility.Public,
                Roles(LeaderRoleId, "Leader", true, CompanionRoleId, "Companion", false),
                audienceId: PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                normIds: new[] { PrototypeSocialNormDefinitionFactory.PromiseKeepingNormId, PrototypeSocialNormDefinitionFactory.WitnessRespectNormId },
                projectionIds: new[] { CompositeProjectionId },
                text: "A temporary but persistent party identity for companions acting together.",
                tagIds: new[] { "prototype", "party" });
            return definition;
        }

        private static SocialGraphProjectionDefinition CreateProjection() => UnityEngine.ScriptableObject.CreateInstance<SocialGraphProjectionDefinition>();
        private static InformalSocialGroupDefinition CreateGroup() => UnityEngine.ScriptableObject.CreateInstance<InformalSocialGroupDefinition>();
        private static SocialGraphEdgeSourceWeightData Weight(SocialGraphEdgeKind kind, int weight) => new SocialGraphEdgeSourceWeightData { edgeKind = kind, authoredWeight = weight };

        private static SocialNetworkGroupRoleDefinitionData[] Roles(params object[] values)
        {
            List<SocialNetworkGroupRoleDefinitionData> roles = new List<SocialNetworkGroupRoleDefinitionData>();
            for (int i = 0; i + 2 < values.Length; i += 3)
            {
                roles.Add(new SocialNetworkGroupRoleDefinitionData
                {
                    roleId = values[i] as string ?? string.Empty,
                    displayName = values[i + 1] as string ?? string.Empty,
                    leaderRole = values[i + 2] is bool leader && leader
                });
            }

            return roles.ToArray();
        }
    }
}
