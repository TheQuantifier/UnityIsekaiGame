using System;

namespace UnityIsekaiGame.Beings
{
    [Flags]
    public enum BeingLocomotionCapabilities
    {
        None = 0,
        Ground = 1 << 0,
        Flying = 1 << 1,
        Swimming = 1 << 2,
        Climbing = 1 << 3,
        Stationary = 1 << 4
    }
}
