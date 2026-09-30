using System;

namespace UnityIsekaiGame.Combat
{
    public static class AttackDeterministicRoll
    {
        public static float FromTransaction(string transactionId, string channel)
        {
            string input = $"{transactionId ?? string.Empty}:{channel ?? string.Empty}";
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= 16777619u;
                }

                return (hash & 0x00FFFFFFu) / 16777216f;
            }
        }

        public static bool IsValidRoll(float roll)
        {
            return !float.IsNaN(roll) && !float.IsInfinity(roll) && roll >= 0f && roll < 1f;
        }

        public static string NewTransactionId(string prefix = "attack")
        {
            return $"{(string.IsNullOrWhiteSpace(prefix) ? "attack" : prefix)}.{Guid.NewGuid():N}";
        }
    }
}
