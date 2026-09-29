using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Influence;

namespace UnityIsekaiGame.Social.Emotions
{
    public static class PrototypeSocialEmotionDefinitionFactory
    {
        public const string MoodValenceId = "mood.prototype.valence";
        public const string MoodArousalId = "mood.prototype.arousal";
        public const string MoodAnxietyId = "mood.prototype.anxiety";
        public const string MoodSocialOpennessId = "mood.prototype.social-openness";
        public const string MoodAggressionId = "mood.prototype.aggression";
        public const string MoodMoraleId = "mood.prototype.morale";

        public const string JoyId = "emotion.prototype.joy";
        public const string SadnessId = "emotion.prototype.sadness";
        public const string AngerId = "emotion.prototype.anger";
        public const string FearId = "emotion.prototype.fear";
        public const string ReliefId = "emotion.prototype.relief";
        public const string GratitudeId = "emotion.prototype.gratitude";
        public const string GuiltId = "emotion.prototype.guilt";
        public const string ShameId = "emotion.prototype.shame";
        public const string PrideId = "emotion.prototype.pride";
        public const string AnxietyId = "emotion.prototype.anxiety";
        public const string DisgustId = "emotion.prototype.disgust";
        public const string EnvyId = "emotion.prototype.envy";
        public const string ResentmentId = "emotion.prototype.resentment";
        public const string HopeId = "emotion.prototype.hope";
        public const string DisappointmentId = "emotion.prototype.disappointment";

        public const string AcceptedGoodNewsRuleId = "emotion-appraisal.prototype.accepted-good-news";
        public const string AcceptedThreatRuleId = "emotion-appraisal.prototype.accepted-threat";
        public const string DetectedDeceptionRuleId = "emotion-appraisal.prototype.detected-deception";
        public const string ReceivedHelpRuleId = "emotion-appraisal.prototype.received-help";
        public const string PublicNormViolationRuleId = "emotion-appraisal.prototype.public-norm-violation";
        public const string SelfCausedHarmRuleId = "emotion-appraisal.prototype.self-caused-harm";
        public const string AchievementRuleId = "emotion-appraisal.prototype.achievement";
        public const string LossRuleId = "emotion-appraisal.prototype.loss";
        public const string DangerEndedRuleId = "emotion-appraisal.prototype.danger-ended";
        public const string PromiseDisappointedRuleId = "emotion-appraisal.prototype.promise-disappointed";

        public static DefinitionRegistry AddMissingPrototypeSocialEmotionDefinitions(DefinitionRegistry registry)
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
            List<ScriptableObject> definitions = new List<ScriptableObject>
            {
                Mood(MoodValenceId, "Mood Valence", SocialMoodDimensionCategory.Valence, -100, 100, 0, 0.05d, true, "Overall pleasant or unpleasant mood."),
                Mood(MoodArousalId, "Mood Arousal", SocialMoodDimensionCategory.Arousal, 0, 100, 20, 0.04d, false, "Overall activation and agitation."),
                Mood(MoodAnxietyId, "Mood Anxiety", SocialMoodDimensionCategory.Anxiety, 0, 100, 0, 0.03d, false, "Persistent anxious tension."),
                Mood(MoodSocialOpennessId, "Social Openness", SocialMoodDimensionCategory.SocialOpenness, -100, 100, 0, 0.04d, true, "Willingness to approach and engage socially."),
                Mood(MoodAggressionId, "Mood Aggression", SocialMoodDimensionCategory.Aggression, 0, 100, 0, 0.05d, false, "Readiness for hostile response."),
                Mood(MoodMoraleId, "Mood Morale", SocialMoodDimensionCategory.Morale, -100, 100, 0, 0.03d, true, "Longer confidence and resolve.")
            };

            definitions.AddRange(new[]
            {
                Emotion(JoyId, "Joy", SocialEmotionCategory.Joy, SocialEmotionValence.Positive, SocialEmotionArousal.Medium, MoodValenceId, 35, 24),
                Emotion(SadnessId, "Sadness", SocialEmotionCategory.Sadness, SocialEmotionValence.Negative, SocialEmotionArousal.Low, MoodValenceId, -35, -10),
                Emotion(AngerId, "Anger", SocialEmotionCategory.Anger, SocialEmotionValence.Negative, SocialEmotionArousal.High, MoodAggressionId, 40, -24),
                Emotion(FearId, "Fear", SocialEmotionCategory.Fear, SocialEmotionValence.Negative, SocialEmotionArousal.High, MoodAnxietyId, 45, -18),
                Emotion(ReliefId, "Relief", SocialEmotionCategory.Relief, SocialEmotionValence.Positive, SocialEmotionArousal.Low, MoodAnxietyId, -35, 12),
                Emotion(GratitudeId, "Gratitude", SocialEmotionCategory.Gratitude, SocialEmotionValence.Positive, SocialEmotionArousal.Medium, MoodSocialOpennessId, 40, 28),
                Emotion(GuiltId, "Guilt", SocialEmotionCategory.Guilt, SocialEmotionValence.Negative, SocialEmotionArousal.Medium, MoodValenceId, -28, -10),
                Emotion(ShameId, "Shame", SocialEmotionCategory.Shame, SocialEmotionValence.Negative, SocialEmotionArousal.Medium, MoodSocialOpennessId, -35, -16),
                Emotion(PrideId, "Pride", SocialEmotionCategory.Pride, SocialEmotionValence.Positive, SocialEmotionArousal.Medium, MoodMoraleId, 40, 18),
                Emotion(AnxietyId, "Anxiety", SocialEmotionCategory.Anxiety, SocialEmotionValence.Negative, SocialEmotionArousal.High, MoodAnxietyId, 42, -14),
                Emotion(DisgustId, "Disgust", SocialEmotionCategory.Disgust, SocialEmotionValence.Negative, SocialEmotionArousal.Medium, MoodSocialOpennessId, -30, -20),
                Emotion(EnvyId, "Envy", SocialEmotionCategory.Envy, SocialEmotionValence.Negative, SocialEmotionArousal.Medium, MoodValenceId, -20, -8),
                Emotion(ResentmentId, "Resentment", SocialEmotionCategory.Resentment, SocialEmotionValence.Negative, SocialEmotionArousal.Medium, MoodAggressionId, 30, -16),
                Emotion(HopeId, "Hope", SocialEmotionCategory.Hope, SocialEmotionValence.Positive, SocialEmotionArousal.Medium, MoodMoraleId, 32, 14),
                Emotion(DisappointmentId, "Disappointment", SocialEmotionCategory.Disappointment, SocialEmotionValence.Negative, SocialEmotionArousal.Low, MoodMoraleId, -34, -12)
            });

            definitions.AddRange(new[]
            {
                Rule(AcceptedGoodNewsRuleId, "Accepted Good News", JoyId, SocialEmotionCauseCategory.BeliefAccepted, SocialEmotionResponsibility.Circumstance, SocialInfluenceTruthStatus.True, SocialInfluenceDetectionOutcome.NotApplicable, 100, 55, 180d, MoodValenceId, 35, 20, new[] { "good-news" }),
                Rule(AcceptedThreatRuleId, "Accepted Threat", FearId, SocialEmotionCauseCategory.BeliefAccepted, SocialEmotionResponsibility.Target, SocialInfluenceTruthStatus.True, SocialInfluenceDetectionOutcome.NotApplicable, 110, 65, 180d, MoodAnxietyId, 45, -18, new[] { "threat" }),
                Rule(DetectedDeceptionRuleId, "Detected Deception", AngerId, SocialEmotionCauseCategory.DeceptionDetected, SocialEmotionResponsibility.Target, SocialInfluenceTruthStatus.False, SocialInfluenceDetectionOutcome.Detected, 140, 70, 240d, MoodAggressionId, 48, -30, new[] { "deception" }),
                Rule(ReceivedHelpRuleId, "Received Help", GratitudeId, SocialEmotionCauseCategory.Interaction, SocialEmotionResponsibility.Target, SocialInfluenceTruthStatus.Unknown, SocialInfluenceDetectionOutcome.NotApplicable, 120, 60, 180d, MoodSocialOpennessId, 45, 30, new[] { "help" }),
                Rule(PublicNormViolationRuleId, "Public Norm Violation", ShameId, SocialEmotionCauseCategory.NormViolation, SocialEmotionResponsibility.Self, SocialInfluenceTruthStatus.Unknown, SocialInfluenceDetectionOutcome.NotApplicable, 115, 55, 210d, MoodSocialOpennessId, -35, -20, new[] { "public" }),
                Rule(SelfCausedHarmRuleId, "Self Caused Harm", GuiltId, SocialEmotionCauseCategory.Interaction, SocialEmotionResponsibility.Self, SocialInfluenceTruthStatus.Unknown, SocialInfluenceDetectionOutcome.NotApplicable, 105, 50, 240d, MoodValenceId, -30, -10, new[] { "harm" }),
                Rule(AchievementRuleId, "Achievement", PrideId, SocialEmotionCauseCategory.Achievement, SocialEmotionResponsibility.Self, SocialInfluenceTruthStatus.True, SocialInfluenceDetectionOutcome.NotApplicable, 100, 60, 240d, MoodMoraleId, 42, 18, new[] { "achievement" }),
                Rule(LossRuleId, "Loss", SadnessId, SocialEmotionCauseCategory.Loss, SocialEmotionResponsibility.Circumstance, SocialInfluenceTruthStatus.True, SocialInfluenceDetectionOutcome.NotApplicable, 100, 60, 240d, MoodValenceId, -38, -10, new[] { "loss" }),
                Rule(DangerEndedRuleId, "Danger Ended", ReliefId, SocialEmotionCauseCategory.Threat, SocialEmotionResponsibility.Circumstance, SocialInfluenceTruthStatus.True, SocialInfluenceDetectionOutcome.NotApplicable, 90, 55, 180d, MoodAnxietyId, -40, 12, new[] { "resolved" }),
                Rule(PromiseDisappointedRuleId, "Promise Disappointed", DisappointmentId, SocialEmotionCauseCategory.Interaction, SocialEmotionResponsibility.Target, SocialInfluenceTruthStatus.True, SocialInfluenceDetectionOutcome.NotApplicable, 100, 55, 210d, MoodMoraleId, -35, -14, new[] { "broken-promise" })
            });

            return definitions;
        }

        private static SocialEmotionDefinition Emotion(string id, string name, SocialEmotionCategory category, SocialEmotionValence valence, SocialEmotionArousal arousal, string moodId, int moodDelta, int decisionDelta)
        {
            SocialEmotionDefinition definition = ScriptableObject.CreateInstance<SocialEmotionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, valence, arousal, SocialEmotionDecayPolicy.Linear, SocialEmotionStackingPolicy.ReinforceExisting, SocialEmotionTargetPolicy.PersonOrSubject, SocialEmotionVisibility.Observable, moodId, 0, 100, 45, 180d, moodDelta, decisionDelta, true, true, new[] { "prototype", "step12", "emotion" });
            return definition;
        }

        private static SocialMoodDimensionDefinition Mood(string id, string name, SocialMoodDimensionCategory category, int minimum, int maximum, int neutral, double recovery, bool negativeAllowed, string description)
        {
            SocialMoodDimensionDefinition definition = ScriptableObject.CreateInstance<SocialMoodDimensionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, minimum, maximum, neutral, recovery, negativeAllowed, description, new[] { "prototype", "step12", "emotion" });
            return definition;
        }

        private static SocialEmotionAppraisalRuleDefinition Rule(string id, string name, string emotionId, SocialEmotionCauseCategory cause, SocialEmotionResponsibility responsibility, SocialInfluenceTruthStatus truth, SocialInfluenceDetectionOutcome detection, int priority, int intensity, double duration, string moodId, int moodDelta, int decisionDelta, IEnumerable<string> requiredTags)
        {
            SocialEmotionAppraisalRuleDefinition definition = ScriptableObject.CreateInstance<SocialEmotionAppraisalRuleDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, emotionId, cause, responsibility, truth, detection, priority, intensity, duration, moodId, moodDelta, decisionDelta, requiredTags, new[] { "prototype", "step12", "emotion" });
            return definition;
        }
    }
}
