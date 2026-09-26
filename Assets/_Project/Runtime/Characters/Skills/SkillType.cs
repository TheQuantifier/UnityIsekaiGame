using System;

namespace UnityIsekaiGame.Skills
{
    [Flags]
    public enum SkillType
    {
        Unknown = 0,
        Combat = 1 << 0,
        Magic = 1 << 1,
        Crafting = 1 << 2,
        Gathering = 1 << 3,
        Commerce = 1 << 4,
        Knowledge = 1 << 5,
        Support = 1 << 6,
        Utility = 1 << 7
    }
}
