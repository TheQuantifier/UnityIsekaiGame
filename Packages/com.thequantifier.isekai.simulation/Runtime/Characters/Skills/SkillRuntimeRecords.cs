using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Skills
{
    [Serializable]
    public sealed class SkillLearningProgressRecord
    {
        public string skillDefinitionId;
        public int currentHiddenCount;
        public int requiredCountSnapshot;
        public string qualifyingEventId;
        public int acquisitionState;
        public string firstProgressAtUtc;
        public double firstProgressAtPlaytimeSeconds;
        public string latestProgressAtUtc;
        public double latestProgressAtPlaytimeSeconds;
        public string sourceSystem;
    }

    [Serializable]
    public sealed class RuntimeSkillRecord
    {
        public string skillDefinitionId;
        public int currentGrade;
        public int currentXp;
        public int lifetimeXp;
        public int lifetimeValidUses;
        public int acquisitionSource;
        public string acquisitionReason;
        public string acquisitionAtUtc;
        public double acquisitionAtPlaytimeSeconds;
        public int startingGrade;
        public string lastUseAtUtc;
        public double lastUseAtPlaytimeSeconds;
        public List<SkillPromotionRecord> promotionHistory = new List<SkillPromotionRecord>();
        public List<string> appliedGradeSourceIds = new List<string>();
        public List<string> unlockedAbilityIds = new List<string>();
        public List<string> unlockedCapabilityIds = new List<string>();
    }

    [Serializable]
    public sealed class SkillPromotionRecord
    {
        public int fromGrade;
        public int toGrade;
        public string promotedAtUtc;
        public double promotedAtPlaytimeSeconds;
        public string source;
        public string reason;
    }

    [Serializable]
    public sealed class PlayerSkillsSaveData
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string personId;
        public List<SkillLearningProgressRecord> hiddenLearningProgress = new List<SkillLearningProgressRecord>();
        public List<RuntimeSkillRecord> learnedSkills = new List<RuntimeSkillRecord>();
        public List<string> consumedActionEventIds = new List<string>();
    }

    public sealed class SkillChangedEventArgs : EventArgs
    {
        public SkillChangedEventArgs(RuntimeSkillRecord skill, SkillGrade oldGrade, SkillGrade newGrade, int xpDelta, bool restoring)
        {
            Skill = skill;
            OldGrade = oldGrade;
            NewGrade = newGrade;
            XpDelta = xpDelta;
            Restoring = restoring;
        }

        public RuntimeSkillRecord Skill { get; }
        public SkillGrade OldGrade { get; }
        public SkillGrade NewGrade { get; }
        public int XpDelta { get; }
        public bool Restoring { get; }
    }
}
