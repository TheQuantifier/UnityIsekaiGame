using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Influence
{
    public static class PrototypeSocialInfluenceDefinitionFactory
    {
        public const string PresentEvidenceId = "social-influence-method.prototype.present-evidence";
        public const string MakeReasonedArgumentId = "social-influence-method.prototype.reasoned-argument";
        public const string AppealToTrustId = "social-influence-method.prototype.appeal-to-trust";
        public const string AppealToLoyaltyId = "social-influence-method.prototype.appeal-to-loyalty";
        public const string AppealToDutyId = "social-influence-method.prototype.appeal-to-duty";
        public const string AppealToAuthorityId = "social-influence-method.prototype.appeal-to-authority";
        public const string ReassureId = "social-influence-method.prototype.reassure";
        public const string InspireId = "social-influence-method.prototype.inspire";
        public const string DiscourageId = "social-influence-method.prototype.discourage";
        public const string PersuadeRequestId = "social-influence-method.prototype.persuade-request";
        public const string IntimidateId = "social-influence-method.prototype.intimidate";
        public const string TellDirectLieId = "social-influence-method.prototype.direct-lie";
        public const string MisleadByOmissionId = "social-influence-method.prototype.mislead-by-omission";
        public const string DenyAccusationId = "social-influence-method.prototype.deny-accusation";
        public const string ConfessTruthId = "social-influence-method.prototype.confess-truth";
        public const string CorrectFalseClaimId = "social-influence-method.prototype.correct-false-claim";
        public const string AskForMoreEvidenceId = "social-influence-method.prototype.ask-for-more-evidence";

        public static DefinitionRegistry AddMissingPrototypeSocialInfluenceDefinitions(DefinitionRegistry registry)
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
            return new[]
            {
                Method(PresentEvidenceId, "Present Evidence", SocialInfluenceCategory.EvidencePresentation, new[] { SocialInfluenceIntent.ChangeBelief, SocialInfluenceIntent.IncreaseBeliefConfidence, SocialInfluenceIntent.CorrectBelief }, 620, 420, 180, 40, 60, 120, 90, 4d, false),
                Method(MakeReasonedArgumentId, "Make Reasoned Argument", SocialInfluenceCategory.RationalPersuasion, new[] { SocialInfluenceIntent.ChangeBelief, SocialInfluenceIntent.GainAgreement, SocialInfluenceIntent.CreateDoubt }, 560, 430, 120, 50, 40, 150, 75, 5d, false),
                Method(AppealToTrustId, "Appeal To Trust", SocialInfluenceCategory.RelationshipAppeal, new[] { SocialInfluenceIntent.GainAgreement, SocialInfluenceIntent.GainCompliance, SocialInfluenceIntent.GainPromise }, 520, 420, 60, 160, 35, 160, 85, 8d, false),
                Method(AppealToLoyaltyId, "Appeal To Loyalty", SocialInfluenceCategory.RelationshipAppeal, new[] { SocialInfluenceIntent.GainCompliance, SocialInfluenceIntent.GainPromise, SocialInfluenceIntent.EncourageAction }, 530, 440, 40, 180, 30, 170, 90, 8d, false),
                Method(AppealToDutyId, "Appeal To Duty", SocialInfluenceCategory.DutyAppeal, new[] { SocialInfluenceIntent.GainCompliance, SocialInfluenceIntent.EncourageAction, SocialInfluenceIntent.DiscourageAction }, 550, 440, 70, 60, 100, 150, 85, 8d, false),
                Method(AppealToAuthorityId, "Appeal To Authority", SocialInfluenceCategory.AuthorityAppeal, new[] { SocialInfluenceIntent.GainCompliance, SocialInfluenceIntent.GainPermission, SocialInfluenceIntent.DiscourageAction }, 570, 450, 70, 50, 150, 140, 95, 10d, false),
                Method(ReassureId, "Reassure", SocialInfluenceCategory.Reassurance, new[] { SocialInfluenceIntent.Reassure, SocialInfluenceIntent.DecreaseBeliefConfidence, SocialInfluenceIntent.RepairCredibility }, 500, 380, 60, 120, 50, 100, 60, 6d, false),
                Method(InspireId, "Inspire", SocialInfluenceCategory.Inspiration, new[] { SocialInfluenceIntent.EncourageAction, SocialInfluenceIntent.GainAgreement }, 540, 420, 60, 90, 100, 110, 110, 8d, false),
                Method(DiscourageId, "Discourage", SocialInfluenceCategory.Discouragement, new[] { SocialInfluenceIntent.DiscourageAction, SocialInfluenceIntent.CreateDoubt }, 540, 430, 70, 60, 90, 130, 110, 8d, false),
                Method(PersuadeRequestId, "Persuade Request", SocialInfluenceCategory.NegotiatedRequest, new[] { SocialInfluenceIntent.GainCompliance, SocialInfluenceIntent.GainPromise, SocialInfluenceIntent.GainPermission }, 560, 450, 80, 100, 70, 150, 100, 8d, false),
                Method(IntimidateId, "Intimidate", SocialInfluenceCategory.Intimidation, new[] { SocialInfluenceIntent.Intimidate, SocialInfluenceIntent.GainCompliance, SocialInfluenceIntent.DiscourageAction }, 600, 460, 20, 40, 130, 190, 130, 12d, false),
                Method(TellDirectLieId, "Tell Direct Lie", SocialInfluenceCategory.Deception, new[] { SocialInfluenceIntent.ChangeBelief, SocialInfluenceIntent.ConcealTruth, SocialInfluenceIntent.AvoidBlame }, 560, 450, 100, 60, 50, 260, 75, 12d, true),
                Method(MisleadByOmissionId, "Mislead By Omission", SocialInfluenceCategory.Omission, new[] { SocialInfluenceIntent.ConcealTruth, SocialInfluenceIntent.CreateDoubt, SocialInfluenceIntent.AvoidBlame }, 530, 430, 80, 70, 50, 210, 65, 10d, true),
                Method(DenyAccusationId, "Deny Accusation", SocialInfluenceCategory.Denial, new[] { SocialInfluenceIntent.AvoidBlame, SocialInfluenceIntent.CreateDoubt, SocialInfluenceIntent.RepairCredibility }, 520, 420, 70, 100, 90, 180, 70, 8d, true),
                Method(ConfessTruthId, "Confess Truth", SocialInfluenceCategory.Confession, new[] { SocialInfluenceIntent.ChangeBelief, SocialInfluenceIntent.RepairCredibility, SocialInfluenceIntent.CorrectBelief }, 600, 380, 130, 120, 90, 80, 80, 8d, false),
                Method(CorrectFalseClaimId, "Correct False Claim", SocialInfluenceCategory.Correction, new[] { SocialInfluenceIntent.CorrectBelief, SocialInfluenceIntent.CreateDoubt, SocialInfluenceIntent.IncreaseBeliefConfidence }, 610, 410, 170, 70, 80, 100, 85, 6d, false),
                Method(AskForMoreEvidenceId, "Ask For More Evidence", SocialInfluenceCategory.RationalPersuasion, new[] { SocialInfluenceIntent.CreateDoubt, SocialInfluenceIntent.DecreaseBeliefConfidence }, 480, 390, 60, 40, 40, 90, 45, 4d, false)
            };
        }

        private static SocialInfluenceMethodDefinition Method(string id, string name, SocialInfluenceCategory category, IEnumerable<SocialInfluenceIntent> intents, int influence, int resistance, int evidence, int relationship, int reputation, int detection, int modifier, double cooldown, bool deception)
        {
            SocialInfluenceMethodDefinition definition = ScriptableObject.CreateInstance<SocialInfluenceMethodDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                category,
                intents,
                new[] { SocialInfluenceSubjectKind.Claim, SocialInfluenceSubjectKind.Person, SocialInfluenceSubjectKind.HistoricalEvent, SocialInfluenceSubjectKind.Rumor, SocialInfluenceSubjectKind.Promise, SocialInfluenceSubjectKind.Decision, SocialInfluenceSubjectKind.Custom },
                influence,
                resistance,
                evidence,
                relationship,
                reputation,
                detection,
                modifier,
                cooldown,
                90d,
                deception,
                true,
                true,
                true,
                new[] { "prototype", "step12", "influence" });
            return definition;
        }
    }
}
