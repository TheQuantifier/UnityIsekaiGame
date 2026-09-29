using System;
using System.Collections.Generic;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;
using UnityEngine;

namespace UnityIsekaiGame.Social.Interactions
{
    public static class PrototypeSocialInteractionDefinitionFactory
    {
        public const string GreetId = "social-interaction.prototype.greet";
        public const string IntroduceId = "social-interaction.prototype.introduce";
        public const string ComplimentId = "social-interaction.prototype.compliment";
        public const string InsultId = "social-interaction.prototype.insult";
        public const string ThankId = "social-interaction.prototype.thank";
        public const string ApologizeId = "social-interaction.prototype.apologize";
        public const string RequestHelpId = "social-interaction.prototype.request-help";
        public const string WarnId = "social-interaction.prototype.warn";
        public const string ThreatenId = "social-interaction.prototype.threaten";
        public const string AccuseId = "social-interaction.prototype.accuse";
        public const string DenyId = "social-interaction.prototype.deny";
        public const string ShareInformationId = "social-interaction.prototype.share-information";
        public const string ReconcileId = "social-interaction.prototype.reconcile";
        public const string PublicPraiseId = "social-interaction.prototype.public-praise";
        public const string PublicCondemnationId = "social-interaction.prototype.public-condemnation";
        public const string PromiseId = "social-interaction.prototype.promise";
        public const string TradeId = "social-interaction.prototype.trade";
        public const string AttackId = "social-interaction.prototype.attack";
        public const string CustomActionId = "social-interaction.prototype.custom";

        public static DefinitionRegistry AddMissingPrototypeSocialInteractionDefinitions(DefinitionRegistry registry)
        {
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (registry != null)
            {
                definitions.AddRange(registry.DefinitionsById.Values);
            }

            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (IGameDefinition definition in definitions)
            {
                if (definition != null)
                {
                    existing.Add(definition.Id);
                }
            }

            foreach (SocialInteractionDefinition definition in CreateDefinitions())
            {
                if (!existing.Contains(definition.Id))
                {
                    definitions.Add(definition);
                }
            }

            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<SocialInteractionDefinition> CreateDefinitions()
        {
            return new[]
            {
                Definition(GreetId, "Greet", SocialInteractionCategory.Greeting, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("greet-trust", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.TrustId, 2, required: false),
                        Memory("greet-memory", required: false)
                    }),
                Definition(IntroduceId, "Introduce", SocialInteractionCategory.Introduction, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("introduce-respect", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.RespectId, 3, required: false),
                        History("introduce-history", required: false)
                    }),
                Definition(ComplimentId, "Compliment", SocialInteractionCategory.PositiveExpression, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("compliment-affection", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.AffectionId, 10),
                        Attitude("compliment-respect", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.RespectId, 5, required: false),
                        Memory("compliment-memory", required: false)
                    }),
                Definition(InsultId, "Insult", SocialInteractionCategory.NegativeExpression, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("insult-affection", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.AffectionId, -12),
                        Attitude("insult-hostility", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.HostilityId, 15)
                    }),
                Definition(ThankId, "Thank", SocialInteractionCategory.PositiveExpression, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("thank-affection", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.AffectionId, 5, required: false),
                        Attitude("thank-respect", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.RespectId, 5, required: false)
                    }),
                Definition(ApologizeId, "Apologize", SocialInteractionCategory.Apology, SocialInteractionOutcome.Pending,
                    requiresResponse: true,
                    allowedResponses: new[] { SocialInteractionResponse.Forgive, SocialInteractionResponse.Reject, SocialInteractionResponse.Ignore },
                    acceptedOutcome: SocialInteractionOutcome.Accepted,
                    refusedOutcome: SocialInteractionOutcome.Refused,
                    consequences: new[]
                    {
                        Attitude("apology-trust", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.TrustId, 8, outcomes: new[] { SocialInteractionOutcome.Accepted }),
                        Attitude("apology-hostility-rejected", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.HostilityId, 5, required: false, outcomes: new[] { SocialInteractionOutcome.Refused })
                    }),
                Definition(RequestHelpId, "Request Help", SocialInteractionCategory.Request, SocialInteractionOutcome.Pending,
                    requiresResponse: true,
                    allowedResponses: new[] { SocialInteractionResponse.Accept, SocialInteractionResponse.Refuse, SocialInteractionResponse.Defer },
                    acceptedOutcome: SocialInteractionOutcome.Accepted,
                    refusedOutcome: SocialInteractionOutcome.Refused,
                    consequences: new[]
                    {
                        Attitude("help-accepted-trust", SocialInteractionRole.Initiator, SocialInteractionRole.Target, PrototypeAttitudeDefinitionFactory.TrustId, 10, outcomes: new[] { SocialInteractionOutcome.Accepted }),
                        Attitude("help-refused-hostility", SocialInteractionRole.Initiator, SocialInteractionRole.Target, PrototypeAttitudeDefinitionFactory.HostilityId, 4, required: false, outcomes: new[] { SocialInteractionOutcome.Refused })
                    }),
                Definition(WarnId, "Warn", SocialInteractionCategory.Warning, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("warn-trust", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.TrustId, 5, required: false),
                        Memory("warn-memory", required: false)
                    }),
                Definition(ThreatenId, "Threaten", SocialInteractionCategory.Threat, SocialInteractionOutcome.Success,
                    visibility: SocialInteractionVisibility.Witnessed,
                    consequences: new[]
                    {
                        Attitude("threat-fear", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.FearId, 20),
                        Attitude("threat-hostility", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.HostilityId, 10),
                        Reputation("threat-danger", SocialInteractionRole.Initiator, PrototypeReputationDefinitionFactory.PerceivedDangerId, 15, onlyWhenWitnessed: true, authenticity: true),
                        Reputation("threat-honor", SocialInteractionRole.Initiator, PrototypeReputationDefinitionFactory.HonorId, -5, onlyWhenWitnessed: true, authenticity: true)
                    }),
                Definition(AccuseId, "Accuse", SocialInteractionCategory.Accusation, SocialInteractionOutcome.Success,
                    visibility: SocialInteractionVisibility.Public,
                    consequences: new[]
                    {
                        Reputation("accuse-notoriety", SocialInteractionRole.Target, PrototypeReputationDefinitionFactory.NotorietyId, 8, onlyWhenPublic: true, authenticity: false),
                        Rumor("accuse-rumor", PrototypeRumorDefinitionFactory.FabricatedAccusationRumorId, onlyWhenPublic: true, required: false)
                    }),
                Definition(DenyId, "Deny", SocialInteractionCategory.Denial, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Reputation("deny-credibility", SocialInteractionRole.Initiator, PrototypeReputationDefinitionFactory.CredibilityId, 3, required: false, authenticity: false)
                    }),
                Definition(ShareInformationId, "Share Information", SocialInteractionCategory.Disclosure, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Rumor("share-rumor", PrototypeRumorDefinitionFactory.PublicNewsRumorId),
                        Memory("share-memory", required: false)
                    }),
                Definition(ReconcileId, "Reconcile", SocialInteractionCategory.Reconciliation, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Relationship("reconcile-friendship", PrototypeRelationshipDefinitionFactory.FriendRelationshipId, required: false),
                        Attitude("reconcile-trust", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.TrustId, 15),
                        Attitude("reconcile-hostility", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.HostilityId, -15)
                    }),
                Definition(PublicPraiseId, "Public Praise", SocialInteractionCategory.PublicStatement, SocialInteractionOutcome.Success,
                    visibility: SocialInteractionVisibility.Public,
                    consequences: new[]
                    {
                        Reputation("praise-esteem", SocialInteractionRole.Target, PrototypeReputationDefinitionFactory.EsteemId, 12, onlyWhenPublic: true),
                        Reputation("praise-renown", SocialInteractionRole.Target, PrototypeReputationDefinitionFactory.RenownId, 5, onlyWhenPublic: true)
                    }),
                Definition(PublicCondemnationId, "Public Condemnation", SocialInteractionCategory.PublicStatement, SocialInteractionOutcome.Success,
                    visibility: SocialInteractionVisibility.Public,
                    consequences: new[]
                    {
                        Reputation("condemn-esteem", SocialInteractionRole.Target, PrototypeReputationDefinitionFactory.EsteemId, -12, onlyWhenPublic: true),
                        Reputation("condemn-notoriety", SocialInteractionRole.Target, PrototypeReputationDefinitionFactory.NotorietyId, 10, onlyWhenPublic: true)
                    }),
                Definition(PromiseId, "Promise", SocialInteractionCategory.Promise, SocialInteractionOutcome.Pending,
                    requiresResponse: true,
                    allowedResponses: new[] { SocialInteractionResponse.Accept, SocialInteractionResponse.Refuse },
                    acceptedOutcome: SocialInteractionOutcome.Accepted,
                    refusedOutcome: SocialInteractionOutcome.Refused,
                    consequences: new[]
                    {
                        Promise("promise-record", outcomes: new[] { SocialInteractionOutcome.Accepted }),
                        Attitude("promise-trust", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.TrustId, 6, required: false, outcomes: new[] { SocialInteractionOutcome.Accepted })
                    }),
                Definition(TradeId, "Trade", SocialInteractionCategory.Custom, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        Attitude("trade-trust", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.TrustId, 1, required: false),
                        Attitude("trade-respect", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.RespectId, 1, required: false),
                        Memory("trade-memory", required: false)
                    }, repeatCooldownSeconds: 0.25d),
                Definition(AttackId, "Attack", SocialInteractionCategory.NegativeExpression, SocialInteractionOutcome.Success,
                    visibility: SocialInteractionVisibility.Witnessed,
                    consequences: new[]
                    {
                        Attitude("attack-hostility", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.HostilityId, 20),
                        Attitude("attack-fear", SocialInteractionRole.Target, SocialInteractionRole.Initiator, PrototypeAttitudeDefinitionFactory.FearId, 10, required: false),
                        Reputation("attack-danger", SocialInteractionRole.Initiator, PrototypeReputationDefinitionFactory.PerceivedDangerId, 4, required: false)
                    }, repeatCooldownSeconds: 0.25d),
                Definition(CustomActionId, "Custom Social Action", SocialInteractionCategory.Custom, SocialInteractionOutcome.Success,
                    consequences: new[]
                    {
                        History("custom-history", required: false)
                    })
            };
        }

        private static SocialInteractionDefinition Definition(
            string id,
            string displayName,
            SocialInteractionCategory category,
            SocialInteractionOutcome baseOutcome,
            bool requiresResponse = false,
            SocialInteractionResponse[] allowedResponses = null,
            SocialInteractionOutcome acceptedOutcome = SocialInteractionOutcome.Accepted,
            SocialInteractionOutcome refusedOutcome = SocialInteractionOutcome.Refused,
            SocialInteractionVisibility visibility = SocialInteractionVisibility.Private,
            SocialInteractionConsequenceDefinitionData[] consequences = null,
            double repeatCooldownSeconds = 2d)
        {
            SocialInteractionDefinition definition = ScriptableObject.CreateInstance<SocialInteractionDefinition>();
            definition.DevelopmentConfigure(
                id,
                displayName,
                category,
                baseOutcome,
                consequences ?? Array.Empty<SocialInteractionConsequenceDefinitionData>(),
                responseRequired: requiresResponse,
                responses: allowedResponses ?? Array.Empty<SocialInteractionResponse>(),
                selfTargetAllowed: false,
                witnessesSupported: true,
                publicAudienceSupported: true,
                visibility: visibility,
                repeatScope: SocialInteractionCooldownScope.InitiatorTargetDefinition,
                repeatCooldownSeconds: repeatCooldownSeconds,
                historyReference: true,
                memoryReference: true,
                requiredIds: Array.Empty<string>(),
                requiredCapabilities: Array.Empty<string>(),
                tagIds: new[] { "prototype", "step12" });
            return definition;
        }

        private static SocialInteractionConsequenceDefinitionData Attitude(string id, SocialInteractionRole actorRole, SocialInteractionRole subjectRole, string dimensionId, int amount, bool required = true, SocialInteractionOutcome[] outcomes = null)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.Attitude,
                operation = SocialConsequenceOperation.AddOrReplaceContribution,
                actorRole = actorRole,
                subjectRole = subjectRole,
                dimensionId = dimensionId,
                amount = amount,
                required = required,
                appliesToOutcomes = outcomes ?? Array.Empty<SocialInteractionOutcome>()
            };
        }

        private static SocialInteractionConsequenceDefinitionData Reputation(string id, SocialInteractionRole subjectRole, string dimensionId, int amount, bool required = true, bool onlyWhenWitnessed = false, bool onlyWhenPublic = false, bool authenticity = true)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.Reputation,
                operation = SocialConsequenceOperation.AddReputationContribution,
                subjectRole = subjectRole,
                dimensionId = dimensionId,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                amount = amount,
                required = required,
                onlyWhenWitnessed = onlyWhenWitnessed,
                onlyWhenPublic = onlyWhenPublic,
                tags = authenticity ? new[] { "verified" } : new[] { "alleged" }
            };
        }

        private static SocialInteractionConsequenceDefinitionData Relationship(string id, string relationshipDefinitionId, bool required = true)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.Relationship,
                operation = SocialConsequenceOperation.CreateRelationship,
                actorRole = SocialInteractionRole.Initiator,
                subjectRole = SocialInteractionRole.Target,
                relationshipDefinitionId = relationshipDefinitionId,
                required = required
            };
        }

        private static SocialInteractionConsequenceDefinitionData Rumor(string id, string rumorDefinitionId, bool required = true, bool onlyWhenPublic = false)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.Rumor,
                operation = SocialConsequenceOperation.TransmitRumor,
                actorRole = SocialInteractionRole.Initiator,
                subjectRole = SocialInteractionRole.Target,
                rumorDefinitionId = rumorDefinitionId,
                rumorChannelId = PrototypeRumorDefinitionFactory.ConversationChannelId,
                required = required,
                onlyWhenPublic = onlyWhenPublic
            };
        }

        private static SocialInteractionConsequenceDefinitionData Memory(string id, bool required = true)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.Memory,
                operation = SocialConsequenceOperation.CreateMemoryReference,
                actorRole = SocialInteractionRole.Target,
                subjectRole = SocialInteractionRole.Initiator,
                required = required
            };
        }

        private static SocialInteractionConsequenceDefinitionData History(string id, bool required = true)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.History,
                operation = SocialConsequenceOperation.CreateHistoryReference,
                actorRole = SocialInteractionRole.Initiator,
                subjectRole = SocialInteractionRole.Target,
                required = required
            };
        }

        private static SocialInteractionConsequenceDefinitionData Promise(string id, SocialInteractionOutcome[] outcomes)
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialConsequenceTargetRuntime.Promise,
                operation = SocialConsequenceOperation.CreatePromise,
                actorRole = SocialInteractionRole.Initiator,
                subjectRole = SocialInteractionRole.Target,
                required = true,
                appliesToOutcomes = outcomes ?? Array.Empty<SocialInteractionOutcome>()
            };
        }
    }
}
