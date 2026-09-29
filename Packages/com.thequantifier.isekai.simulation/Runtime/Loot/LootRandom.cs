using UnityEngine;

namespace UnityIsekaiGame.Loot
{
    public interface ILootRandom
    {
        float Next01();
        int RangeInclusive(int minimum, int maximum);
    }

    public sealed class UnityLootRandom : ILootRandom
    {
        public float Next01()
        {
            return Random.value;
        }

        public int RangeInclusive(int minimum, int maximum)
        {
            return Random.Range(minimum, maximum + 1);
        }
    }
}
