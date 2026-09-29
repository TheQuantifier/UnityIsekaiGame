using System;

namespace UnityIsekaiGame.Stats
{
    public interface IActorStats : IRuntimeCalculatedStatReceiver
    {
        float MaximumHealth { get; }
        float MaximumStamina { get; }
        float MaximumMana { get; }
        float AttackPower { get; }
        float Defense { get; }
        float MovementSpeed { get; }
        event Action StatsChanged;
    }
}
