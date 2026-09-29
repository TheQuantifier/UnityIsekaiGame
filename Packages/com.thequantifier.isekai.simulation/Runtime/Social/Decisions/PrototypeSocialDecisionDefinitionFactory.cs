using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Social.Decisions
{
    public static class PrototypeSocialDecisionDefinitionFactory
    {
        public const string ConsiderTrustId = "social-consideration.prototype.trust";
        public const string ConsiderAffectionId = "social-consideration.prototype.affection";
        public const string ConsiderRespectId = "social-consideration.prototype.respect";
        public const string ConsiderFearId = "social-consideration.prototype.fear";
        public const string ConsiderHostilityId = "social-consideration.prototype.hostility";
        public const string ConsiderRelationshipId = "social-consideration.prototype.relationship-exists";
        public const string ConsiderSharedGroupId = "social-consideration.prototype.shared-group";
        public const string ConsiderTargetIsolationId = "social-consideration.prototype.target-isolation";
        public const string ConsiderReputationEsteemId = "social-consideration.prototype.reputation-esteem";
        public const string ConsiderReputationDangerId = "social-consideration.prototype.reputation-danger";
        public const string ConsiderPendingRequestId = "social-consideration.prototype.pending-request";
        public const string ConsiderScriptedPriorityId = "social-consideration.prototype.scripted-priority";

        public const string GreetKnownPersonId = "social-intention.prototype.greet-known-person";
        public const string IntroduceSelfId = "social-intention.prototype.introduce-self";
        public const string MaintainPositiveRelationshipId = "social-intention.prototype.maintain-positive-relationship";
        public const string ThankRecentHelperId = "social-intention.prototype.thank-recent-helper";
        public const string ApologizeForRecentOffenseId = "social-intention.prototype.apologize-for-recent-offense";
        public const string RespondToApologyId = "social-intention.prototype.respond-to-apology";
        public const string AttemptReconciliationId = "social-intention.prototype.attempt-reconciliation";
        public const string RequestHelpId = "social-intention.prototype.request-help";
        public const string OfferHelpId = "social-intention.prototype.offer-help";
        public const string WarnTrustedPersonId = "social-intention.prototype.warn-trusted-person";
        public const string ShareUsefulInformationId = "social-intention.prototype.share-useful-information";
        public const string ShareRumorId = "social-intention.prototype.share-rumor";
        public const string ProtectConfidentialInformationId = "social-intention.prototype.protect-confidential-information";
        public const string ConfrontAccusedPersonId = "social-intention.prototype.confront-accused-person";
        public const string ThreatenHostilePersonId = "social-intention.prototype.threaten-hostile-person";
        public const string PubliclyPraisePersonId = "social-intention.prototype.publicly-praise-person";
        public const string PubliclyCondemnPersonId = "social-intention.prototype.publicly-condemn-person";
        public const string SupportIsolatedGroupMemberId = "social-intention.prototype.support-isolated-group-member";
        public const string AvoidFearedPersonId = "social-intention.prototype.avoid-feared-person";
        public const string RespondToPendingSocialRequestId = "social-intention.prototype.respond-to-pending-social-request";

        public const string SociallyNeutralProfileId = "social-decision-profile.prototype.socially-neutral-npc";
        public const string SociableProfileId = "social-decision-profile.prototype.sociable-npc";
        public const string ReservedProfileId = "social-decision-profile.prototype.reserved-npc";
        public const string SupportiveGroupMemberProfileId = "social-decision-profile.prototype.supportive-group-member";
        public const string CautiousInformationSharerProfileId = "social-decision-profile.prototype.cautious-information-sharer";
        public const string ConflictProneProfileId = "social-decision-profile.prototype.conflict-prone-npc";
        public const string ReconciliationFocusedProfileId = "social-decision-profile.prototype.reconciliation-focused-npc";
        public const string ScriptControlledProfileId = "social-decision-profile.prototype.script-controlled-social-actor";

        public static DefinitionRegistry AddMissingPrototypeSocialDecisionDefinitions(DefinitionRegistry registry)
        {
            List<IGameDefinition> definitions = registry == null ? new List<IGameDefinition>() : registry.DefinitionsById.Values.ToList();
            HashSet<string> existing = new HashSet<string>(definitions.Select(definition => definition.Id), StringComparer.Ordinal);
            foreach (IGameDefinition definition in CreateDefinitions().OfType<IGameDefinition>())
            {
                if (!existing.Contains(definition.Id))
                {
                    definitions.Add(definition);
                }
            }

            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            List<ScriptableObject> definitions = new List<ScriptableObject>();
            definitions.AddRange(CreateConsiderations());
            definitions.AddRange(CreateIntentions());
            definitions.AddRange(CreateProfiles());
            return definitions;
        }

        private static IEnumerable<SocialConsiderationDefinition> CreateConsiderations()
        {
            yield return Consideration(ConsiderTrustId, "Trust Toward Target", SocialDecisionConsiderationInput.TrustTowardTarget, -100, 100, 80);
            yield return Consideration(ConsiderAffectionId, "Affection Toward Target", SocialDecisionConsiderationInput.AffectionTowardTarget, -100, 100, 60);
            yield return Consideration(ConsiderRespectId, "Respect Toward Target", SocialDecisionConsiderationInput.RespectTowardTarget, -100, 100, 45);
            yield return Consideration(ConsiderFearId, "Fear Toward Target", SocialDecisionConsiderationInput.FearTowardTarget, 0, 100, 85);
            yield return Consideration(ConsiderHostilityId, "Hostility Toward Target", SocialDecisionConsiderationInput.HostilityTowardTarget, 0, 100, 75);
            yield return Consideration(ConsiderRelationshipId, "Relationship Exists", SocialDecisionConsiderationInput.RelationshipExists, 0, 100, 70);
            yield return Consideration(ConsiderSharedGroupId, "Shared Group", SocialDecisionConsiderationInput.SharedGroupMembership, 0, 100, 45);
            yield return Consideration(ConsiderTargetIsolationId, "Target Isolation", SocialDecisionConsiderationInput.TargetIsolation, 0, 100, 65);
            yield return Consideration(ConsiderReputationEsteemId, "Reputation Esteem", SocialDecisionConsiderationInput.ReputationEsteem, -100, 100, 35);
            yield return Consideration(ConsiderReputationDangerId, "Reputation Danger", SocialDecisionConsiderationInput.ReputationDanger, -100, 100, 55);
            yield return Consideration(ConsiderPendingRequestId, "Pending Request", SocialDecisionConsiderationInput.PendingRequest, 0, 100, 100);
            yield return Consideration(ConsiderScriptedPriorityId, "Scripted Priority", SocialDecisionConsiderationInput.ScriptedPriority, 0, 100, 100);
        }

        private static IEnumerable<SocialIntentionDefinition> CreateIntentions()
        {
            yield return Intention(GreetKnownPersonId, "Greet Known Person", SocialIntentionCategory.Affiliate, 130, new[] { PrototypeSocialInteractionDefinitionFactory.GreetId }, ConsiderRelationshipId, ConsiderTrustId);
            yield return Intention(IntroduceSelfId, "Introduce Self", SocialIntentionCategory.Affiliate, 90, new[] { PrototypeSocialInteractionDefinitionFactory.IntroduceId }, ConsiderScriptedPriorityId);
            yield return Intention(MaintainPositiveRelationshipId, "Maintain Positive Relationship", SocialIntentionCategory.MaintainRelationship, 115, new[] { PrototypeSocialInteractionDefinitionFactory.ComplimentId }, ConsiderAffectionId, ConsiderRespectId);
            yield return Intention(ThankRecentHelperId, "Thank Recent Helper", SocialIntentionCategory.MaintainRelationship, 120, new[] { PrototypeSocialInteractionDefinitionFactory.ThankId }, ConsiderTrustId, ConsiderAffectionId);
            yield return Intention(ApologizeForRecentOffenseId, "Apologize For Recent Offense", SocialIntentionCategory.Apologize, 150, new[] { PrototypeSocialInteractionDefinitionFactory.ApologizeId }, ConsiderRelationshipId, ConsiderHostilityId);
            yield return Intention(RespondToApologyId, "Respond To Apology", SocialIntentionCategory.RespondToInteraction, 145, new[] { PrototypeSocialInteractionDefinitionFactory.ApologizeId }, ConsiderPendingRequestId, ConsiderTrustId);
            yield return Intention(AttemptReconciliationId, "Attempt Reconciliation", SocialIntentionCategory.Reconcile, 135, new[] { PrototypeSocialInteractionDefinitionFactory.ReconcileId }, ConsiderRelationshipId, ConsiderHostilityId);
            yield return Intention(RequestHelpId, "Request Help", SocialIntentionCategory.SeekSupport, 110, new[] { PrototypeSocialInteractionDefinitionFactory.RequestHelpId }, ConsiderTrustId, ConsiderRelationshipId);
            yield return Intention(OfferHelpId, "Offer Help", SocialIntentionCategory.OfferSupport, 100, new[] { PrototypeSocialInteractionDefinitionFactory.PromiseId }, ConsiderAffectionId, ConsiderSharedGroupId);
            yield return Intention(WarnTrustedPersonId, "Warn Trusted Person", SocialIntentionCategory.Warn, 125, new[] { PrototypeSocialInteractionDefinitionFactory.WarnId }, ConsiderTrustId, ConsiderRelationshipId);
            yield return Intention(ShareUsefulInformationId, "Share Useful Information", SocialIntentionCategory.ShareInformation, 105, new[] { PrototypeSocialInteractionDefinitionFactory.ShareInformationId }, ConsiderTrustId);
            yield return Intention(ShareRumorId, "Share Rumor", SocialIntentionCategory.ShareInformation, 95, new[] { PrototypeSocialInteractionDefinitionFactory.ShareInformationId }, ConsiderTrustId, ConsiderReputationEsteemId);
            yield return Intention(ProtectConfidentialInformationId, "Protect Confidential Information", SocialIntentionCategory.ProtectSecret, 125, Array.Empty<string>(), noInteraction: true, considerations: new[] { ConsiderTrustId });
            yield return Intention(ConfrontAccusedPersonId, "Confront Accused Person", SocialIntentionCategory.Confront, 120, new[] { PrototypeSocialInteractionDefinitionFactory.AccuseId }, ConsiderReputationDangerId, ConsiderHostilityId);
            yield return Intention(ThreatenHostilePersonId, "Threaten Hostile Person", SocialIntentionCategory.Threaten, 130, new[] { PrototypeSocialInteractionDefinitionFactory.ThreatenId }, ConsiderHostilityId, ConsiderFearId);
            yield return Intention(PubliclyPraisePersonId, "Publicly Praise Person", SocialIntentionCategory.ImproveStanding, 100, new[] { PrototypeSocialInteractionDefinitionFactory.PublicPraiseId }, ConsiderReputationEsteemId, ConsiderRespectId);
            yield return Intention(PubliclyCondemnPersonId, "Publicly Condemn Person", SocialIntentionCategory.DefendReputation, 100, new[] { PrototypeSocialInteractionDefinitionFactory.PublicCondemnationId }, ConsiderHostilityId, ConsiderReputationDangerId);
            yield return Intention(SupportIsolatedGroupMemberId, "Support Isolated Group Member", SocialIntentionCategory.SupportGroup, 140, new[] { PrototypeSocialInteractionDefinitionFactory.ComplimentId, PrototypeSocialInteractionDefinitionFactory.PromiseId }, ConsiderSharedGroupId, ConsiderTargetIsolationId);
            yield return Intention(AvoidFearedPersonId, "Avoid Feared Person", SocialIntentionCategory.AvoidPerson, 160, Array.Empty<string>(), noInteraction: true, considerations: new[] { ConsiderFearId, ConsiderHostilityId });
            yield return Intention(RespondToPendingSocialRequestId, "Respond To Pending Social Request", SocialIntentionCategory.RespondToInteraction, 155, new[] { PrototypeSocialInteractionDefinitionFactory.PromiseId, PrototypeSocialInteractionDefinitionFactory.RequestHelpId }, ConsiderPendingRequestId, ConsiderTrustId);
        }

        private static IEnumerable<SocialDecisionProfileDefinition> CreateProfiles()
        {
            string[] all = CreateIntentions().Select(item => item.Id).ToArray();
            yield return Profile(SociallyNeutralProfileId, "Socially Neutral NPC", all, new[] { ConsiderTrustId, ConsiderRelationshipId }, 15d, 8, 8, 24, 100, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(SociableProfileId, "Sociable NPC", new[] { GreetKnownPersonId, IntroduceSelfId, MaintainPositiveRelationshipId, ThankRecentHelperId, PubliclyPraisePersonId }, new[] { ConsiderTrustId, ConsiderAffectionId, ConsiderRelationshipId }, 8d, 10, 8, 30, 90, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(ReservedProfileId, "Reserved NPC", new[] { GreetKnownPersonId, ProtectConfidentialInformationId, AvoidFearedPersonId }, new[] { ConsiderTrustId, ConsiderFearId }, 25d, 4, 4, 12, 130, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(SupportiveGroupMemberProfileId, "Supportive Group Member", new[] { SupportIsolatedGroupMemberId, OfferHelpId, WarnTrustedPersonId, MaintainPositiveRelationshipId }, new[] { ConsiderSharedGroupId, ConsiderTargetIsolationId, ConsiderTrustId }, 10d, 8, 6, 24, 95, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(CautiousInformationSharerProfileId, "Cautious Information Sharer", new[] { ShareUsefulInformationId, ShareRumorId, ProtectConfidentialInformationId, WarnTrustedPersonId }, new[] { ConsiderTrustId, ConsiderReputationEsteemId, ConsiderRelationshipId }, 18d, 6, 5, 18, 120, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(ConflictProneProfileId, "Conflict Prone NPC", new[] { ThreatenHostilePersonId, ConfrontAccusedPersonId, PubliclyCondemnPersonId, AvoidFearedPersonId }, new[] { ConsiderHostilityId, ConsiderFearId, ConsiderReputationDangerId }, 12d, 8, 5, 20, 100, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(ReconciliationFocusedProfileId, "Reconciliation Focused NPC", new[] { ApologizeForRecentOffenseId, AttemptReconciliationId, RespondToApologyId, ThankRecentHelperId }, new[] { ConsiderRelationshipId, ConsiderTrustId, ConsiderHostilityId }, 12d, 6, 5, 20, 95, SocialDecisionExecutionMode.EvaluateOnly);
            yield return Profile(ScriptControlledProfileId, "Script Controlled Social Actor", all, new[] { ConsiderScriptedPriorityId, ConsiderTrustId }, 0d, 12, 10, 32, 1, SocialDecisionExecutionMode.AwaitExternalApproval, playersAllowed: true);
        }

        private static SocialConsiderationDefinition Consideration(string id, string name, SocialDecisionConsiderationInput input, int minimum, int maximum, int weight)
        {
            SocialConsiderationDefinition definition = ScriptableObject.CreateInstance<SocialConsiderationDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, input, SocialDecisionResponseCurve.Linear, minimum, maximum, weight, SocialDecisionMissingDataPolicy.Neutral, false, new[] { "prototype", "step12", "decision" });
            return definition;
        }

        private static SocialIntentionDefinition Intention(string id, string name, SocialIntentionCategory category, int priority, IEnumerable<string> interactions, params string[] considerations)
        {
            return Intention(id, name, category, priority, interactions, false, considerations);
        }

        private static SocialIntentionDefinition Intention(string id, string name, SocialIntentionCategory category, int priority, IEnumerable<string> interactions, bool noInteraction, IEnumerable<string> considerations)
        {
            SocialIntentionDefinition definition = ScriptableObject.CreateInstance<SocialIntentionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, interactions, priority, 8d, true, noInteraction, considerations, new[] { "prototype", "step12", "decision" });
            return definition;
        }

        private static SocialDecisionProfileDefinition Profile(string id, string name, IEnumerable<string> intentions, IEnumerable<string> considerations, double interval, int maxTargets, int maxIntentions, int maxCandidates, int threshold, SocialDecisionExecutionMode mode, bool playersAllowed = false)
        {
            SocialDecisionProfileDefinition definition = ScriptableObject.CreateInstance<SocialDecisionProfileDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, intentions, considerations, interval, maxTargets, maxIntentions, maxCandidates, threshold, mode, playersAllowed, new[] { "prototype", "step12", "decision" });
            return definition;
        }
    }
}
