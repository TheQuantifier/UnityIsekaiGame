using System;

namespace UnityIsekaiGame.ActorLifecycle
{
    public interface IActorLifecycleUtcClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemActorLifecycleUtcClock : IActorLifecycleUtcClock
    {
        public static readonly SystemActorLifecycleUtcClock Instance = new SystemActorLifecycleUtcClock();

        private SystemActorLifecycleUtcClock()
        {
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
