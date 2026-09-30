using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Norms
{
    public static class PrototypeSocialNormDefinitionFactory
    {
        public const string HostGreetingNormId = "social-norm.prototype.host-greeting";
        public const string PublicInsultNormId = "social-norm.prototype.public-insult";
        public const string PrivateInsultNormId = "social-norm.prototype.private-insult";
        public const string IgnoranceMitigatedEtiquetteNormId = "social-norm.prototype.ignorance-mitigated-etiquette";
        public const string WitnessRespectNormId = "social-norm.prototype.witness-respect";
        public const string EmergencyDisclosureNormId = "social-norm.prototype.emergency-disclosure";
        public const string PromiseKeepingNormId = "social-norm.prototype.promise-keeping";
        public const string PraiseEnemyConflictNormId = "social-norm.prototype.praise-enemy-conflict";
        public const string HospitalityOverrideNormId = "social-norm.prototype.hospitality-override";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            return new ScriptableObject[]
            {
                HostGreeting(),
                PrivateInsult(),
                PublicInsult(),
                IgnoranceMitigated(),
                WitnessRespect(),
                EmergencyDisclosure(),
                PromiseKeeping(),
                PraiseEnemyConflict(),
                HospitalityOverride()
            };
        }

        public static DefinitionRegistry AddMissingPrototypeSocialNormDefinitions(DefinitionRegistry baseRegistry)
        {
            IGameDefinition[] existing = baseRegistry == null
                ? new IGameDefinition[0]
                : baseRegistry.DefinitionsById.Values.ToArray();
            IGameDefinition[] additions = CreateDefinitions()
                .OfType<IGameDefinition>()
                .Where(definition => baseRegistry == null || !baseRegistry.Contains(definition.Id))
                .ToArray();
            return new DefinitionRegistry(existing.Concat(additions));
        }

        private static SocialNormDefinition HostGreeting()
        {
            return Norm(
                HostGreetingNormId,
                "Prototype Host Greeting",
                SocialNormCategory.Greeting,
                SocialNormScope.PlaceBased,
                SocialNormConductStrength.StronglyExpected,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.MinorViolation,
                8,
                30,
                40,
                targetRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.GreetId,
                conditions: new[]
                {
                    Condition("place-court", placeId: "place.prototype.court"),
                    Condition("host-context", requiredTag: "host-context")
                },
                consequences: new[]
                {
                    Attitude("host-respect", PrototypeAttitudeDefinitionFactory.RespectId, -4, SocialNormAssessmentClassification.MinorViolation),
                    Reputation("public-rudeness", PrototypeReputationDefinitionFactory.EsteemId, -2, SocialNormAssessmentClassification.MinorViolation)
                },
                text: "Visitors in a formal host context are expected to greet the host.");
        }

        private static SocialNormDefinition PrivateInsult()
        {
            return Norm(
                PrivateInsultNormId,
                "Prototype Private Insult",
                SocialNormCategory.PublicConduct,
                SocialNormScope.Global,
                SocialNormConductStrength.Discouraged,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.MinorViolation,
                6,
                20,
                20,
                targetRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.InsultId,
                conditions: new[] { Visibility("private-only", SocialInteractionVisibility.Private) },
                consequences: new[] { Attitude("private-hostility", PrototypeAttitudeDefinitionFactory.HostilityId, 4, SocialNormAssessmentClassification.MinorViolation) },
                text: "Insults in private are rude but have limited public standing by default.");
        }

        private static SocialNormDefinition PublicInsult()
        {
            return Norm(
                PublicInsultNormId,
                "Prototype Public Insult",
                SocialNormCategory.PublicConduct,
                SocialNormScope.AudienceBased,
                SocialNormConductStrength.StronglyDiscouraged,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.Violation,
                18,
                50,
                45,
                targetRequired: true,
                witnessRequired: true,
                publicRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.InsultId,
                conditions: new[]
                {
                    Visibility("public-only", SocialInteractionVisibility.Public),
                    Condition("public-audience", audienceId: PrototypeReputationDefinitionFactory.GlobalPublicAudienceId)
                },
                consequences: new[]
                {
                    Attitude("witness-hostility", PrototypeAttitudeDefinitionFactory.HostilityId, 10, SocialNormAssessmentClassification.Violation, observersOnly: true),
                    Reputation("public-esteem-loss", PrototypeReputationDefinitionFactory.EsteemId, -8, SocialNormAssessmentClassification.Violation, publicOnly: true)
                },
                text: "Public insults witnessed by an audience carry stronger social consequences.");
        }

        private static SocialNormDefinition IgnoranceMitigated()
        {
            return Norm(
                IgnoranceMitigatedEtiquetteNormId,
                "Prototype Ignorance Mitigated Etiquette",
                SocialNormCategory.CulturalCustom,
                SocialNormScope.Cultural,
                SocialNormConductStrength.Required,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.Violation,
                14,
                35,
                35,
                targetRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.CustomActionId,
                conditions: new[] { Condition("culture-tag", requiredTag: "culture.prototype.formal") },
                exceptionRules: new[] { Exception("ignorance", SocialNormExceptionKind.IgnoranceMitigation, SocialNormExceptionEffect.ReduceSeverity, -8, requiredTag: "actor-unaware") },
                consequences: new[] { Attitude("respect-loss", PrototypeAttitudeDefinitionFactory.RespectId, -6, SocialNormAssessmentClassification.Violation, SocialNormAssessmentClassification.MinorViolation) },
                text: "Ignorance can reduce blame without erasing objective applicability.");
        }

        private static SocialNormDefinition WitnessRespect()
        {
            return Norm(
                WitnessRespectNormId,
                "Prototype Witness Respect",
                SocialNormCategory.RespectAndDeference,
                SocialNormScope.RoleBased,
                SocialNormConductStrength.Encouraged,
                SocialNormAssessmentClassification.Exceeded,
                SocialNormAssessmentClassification.NotApplicable,
                4,
                15,
                25,
                witnessRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.ComplimentId,
                conditions: new[] { Condition("requires-respect-context", requiredTag: "respect-context") },
                consequences: new[] { Attitude("observer-respect", PrototypeAttitudeDefinitionFactory.RespectId, 3, SocialNormAssessmentClassification.Exceeded, observersOnly: true) },
                text: "Respectful conduct can be interpreted separately by each witness.");
        }

        private static SocialNormDefinition EmergencyDisclosure()
        {
            return Norm(
                EmergencyDisclosureNormId,
                "Prototype Emergency Disclosure Exception",
                SocialNormCategory.Confidentiality,
                SocialNormScope.Global,
                SocialNormConductStrength.Prohibited,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.SeriousViolation,
                25,
                60,
                30,
                witnessRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.ShareInformationId,
                conditions: new[] { Condition("secret-tag", requiredTag: "secret-subject") },
                exceptionRules: new[] { Exception("emergency", SocialNormExceptionKind.Emergency, SocialNormExceptionEffect.ExcuseViolation, -20, requiredTag: "emergency") },
                consequences: new[] { Reputation("secret-leak", PrototypeReputationDefinitionFactory.CredibilityId, -12, SocialNormAssessmentClassification.SeriousViolation, publicOnly: true) },
                text: "Secrets should not be discussed publicly unless an explicit emergency exception applies.");
        }

        private static SocialNormDefinition PromiseKeeping()
        {
            return Norm(
                PromiseKeepingNormId,
                "Prototype Promise Keeping",
                SocialNormCategory.PromiseAndObligation,
                SocialNormScope.RelationshipBased,
                SocialNormConductStrength.Required,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.Violation,
                20,
                55,
                50,
                targetRequired: true,
                promiseState: SocialPromiseStatus.Breached.ToString(),
                conditions: new[] { Condition("promise-reference", requiredTag: "promise-context") },
                consequences: new[]
                {
                    Attitude("broken-promise-trust", PrototypeAttitudeDefinitionFactory.TrustId, -12, SocialNormAssessmentClassification.Violation),
                    Reputation("broken-promise-reputation", PrototypeReputationDefinitionFactory.CredibilityId, -6, SocialNormAssessmentClassification.Violation)
                },
                text: "Accepted social promises create expectations that can later be assessed when broken.");
        }

        private static SocialNormDefinition PraiseEnemyConflict()
        {
            return Norm(
                PraiseEnemyConflictNormId,
                "Prototype Praise Enemy Conflict",
                SocialNormCategory.SpeechAndDisclosure,
                SocialNormScope.AudienceBased,
                SocialNormConductStrength.Discouraged,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.Disputed,
                10,
                25,
                30,
                witnessRequired: true,
                publicRequired: true,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.PublicPraiseId,
                conditions: new[] { Condition("enemy-audience", requiredTag: "audience.enemy-of-target") },
                overrides: null,
                consequences: new[] { Reputation("disputed-public-praise", PrototypeReputationDefinitionFactory.CredibilityId, -3, SocialNormAssessmentClassification.Disputed, publicOnly: true) },
                text: "An audience may condemn praise for someone it treats as an enemy, but higher-priority customs can override it.");
        }

        private static SocialNormDefinition HospitalityOverride()
        {
            return Norm(
                HospitalityOverrideNormId,
                "Prototype Hospitality Override",
                SocialNormCategory.Hospitality,
                SocialNormScope.Cultural,
                SocialNormConductStrength.Required,
                SocialNormAssessmentClassification.Satisfied,
                SocialNormAssessmentClassification.Violation,
                8,
                80,
                60,
                conditions: new[] { Condition("hospitality-duty", requiredTag: "hospitality-duty") },
                overrides: new[] { PraiseEnemyConflictNormId },
                text: "Hospitality can outrank lower-priority audience discomfort in deterministic conflict checks.");
        }

        private static SocialNormDefinition Norm(
            string id,
            string name,
            SocialNormCategory category,
            SocialNormScope scope,
            SocialNormConductStrength strength,
            SocialNormAssessmentClassification satisfied,
            SocialNormAssessmentClassification violated,
            int severity,
            int priority,
            int specificity,
            bool targetRequired = false,
            bool witnessRequired = false,
            bool publicRequired = false,
            string interactionDefinitionId = "",
            string promiseState = "",
            IEnumerable<SocialNormContextConditionData> conditions = null,
            IEnumerable<SocialNormExceptionDefinitionData> exceptionRules = null,
            IEnumerable<SocialNormConsequenceDefinitionData> consequences = null,
            IEnumerable<string> overrides = null,
            string text = "")
        {
            SocialNormDefinition definition = ScriptableObject.CreateInstance<SocialNormDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, scope, strength, satisfied, violated, severity, priority, specificity, targetRequired, witnessRequired, publicRequired, interactionDefinitionId, promiseState, conditions, exceptionRules, consequences, overrides, new[] { "prototype", "alpha", "social", "norm" }, text);
            return definition;
        }

        private static SocialNormContextConditionData Condition(string id, string actorRoleId = "", string targetRoleId = "", string relationshipDefinitionId = "", string placeId = "", string audienceId = "", string requiredTag = "", bool requiresWitness = false, bool optional = false)
        {
            return new SocialNormContextConditionData
            {
                conditionId = id,
                actorRoleId = actorRoleId,
                targetRoleId = targetRoleId,
                relationshipDefinitionId = relationshipDefinitionId,
                placeId = placeId,
                audienceId = audienceId,
                requiredTag = requiredTag,
                requiresWitness = requiresWitness,
                optional = optional
            };
        }

        private static SocialNormContextConditionData Visibility(string id, SocialInteractionVisibility visibility)
        {
            SocialNormContextConditionData condition = Condition(id);
            condition.hasVisibility = true;
            condition.visibility = visibility;
            return condition;
        }

        private static SocialNormExceptionDefinitionData Exception(string id, SocialNormExceptionKind kind, SocialNormExceptionEffect effect, int severityDelta, string requiredTag)
        {
            return new SocialNormExceptionDefinitionData
            {
                exceptionId = id,
                kind = kind,
                effect = effect,
                severityDelta = severityDelta,
                requiredTag = requiredTag
            };
        }

        private static SocialNormConsequenceDefinitionData Attitude(string id, string dimensionId, int amount, params SocialNormAssessmentClassification[] classifications)
        {
            return Attitude(id, dimensionId, amount, classifications ?? new[] { SocialNormAssessmentClassification.Violation }, observersOnly: false);
        }

        private static SocialNormConsequenceDefinitionData Attitude(string id, string dimensionId, int amount, SocialNormAssessmentClassification classification, bool observersOnly = false)
        {
            return Attitude(id, dimensionId, amount, new[] { classification }, observersOnly);
        }

        private static SocialNormConsequenceDefinitionData Attitude(string id, string dimensionId, int amount, SocialNormAssessmentClassification[] classifications, bool observersOnly = false)
        {
            return new SocialNormConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialNormConsequenceTargetRuntime.InterpersonalAttitude,
                operation = SocialNormConsequenceOperation.AddOrReplaceAttitudeContribution,
                policy = SocialNormConsequencePolicy.Required,
                dimensionId = dimensionId,
                amount = amount,
                appliesToClassifications = classifications,
                observersOnly = observersOnly
            };
        }

        private static SocialNormConsequenceDefinitionData Reputation(string id, string dimensionId, int amount, SocialNormAssessmentClassification classification, bool publicOnly = false)
        {
            return new SocialNormConsequenceDefinitionData
            {
                consequenceId = id,
                targetRuntime = SocialNormConsequenceTargetRuntime.Reputation,
                operation = SocialNormConsequenceOperation.AddOrReplaceReputationContribution,
                policy = SocialNormConsequencePolicy.Required,
                dimensionId = dimensionId,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                amount = amount,
                appliesToClassifications = new[] { classification },
                publicOnly = publicOnly
            };
        }
    }
}
