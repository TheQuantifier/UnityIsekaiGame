using System;

namespace UnityIsekaiGame.Beings
{
    [Flags]
    public enum BeingNatureFlags
    {
        None = 0,
        Living = 1 << 0,
        Artificial = 1 << 1,
        Spiritual = 1 << 2,
        Summoned = 1 << 3
    }
}
