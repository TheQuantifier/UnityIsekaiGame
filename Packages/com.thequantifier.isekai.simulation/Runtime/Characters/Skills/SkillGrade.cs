using System;

namespace UnityIsekaiGame.Skills
{
    public enum SkillGrade
    {
        F = 0,
        E = 1,
        D = 2,
        C = 3,
        B = 4,
        A = 5,
        AA = 6,
        AAA = 7
    }

    public static class SkillGradeUtility
    {
        public const int MinimumIndex = 0;
        public const int MaximumIndex = 7;

        public static bool IsValid(SkillGrade grade)
        {
            int value = (int)grade;
            return value >= MinimumIndex && value <= MaximumIndex;
        }

        public static bool IsMastered(SkillGrade grade)
        {
            return grade == SkillGrade.AAA;
        }

        public static int ToIndex(SkillGrade grade)
        {
            return IsValid(grade) ? (int)grade : MinimumIndex;
        }

        public static string DisplayLabel(SkillGrade grade)
        {
            return IsValid(grade) ? grade.ToString() : SkillGrade.F.ToString();
        }

        public static bool TryGetNext(SkillGrade grade, out SkillGrade next)
        {
            next = grade;
            if (!IsValid(grade) || IsMastered(grade))
            {
                return false;
            }

            next = (SkillGrade)((int)grade + 1);
            return true;
        }

        public static SkillGrade Clamp(SkillGrade grade)
        {
            int value = Math.Max(MinimumIndex, Math.Min(MaximumIndex, (int)grade));
            return (SkillGrade)value;
        }
    }
}
