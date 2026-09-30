using System;
using UnityEngine;

namespace UnityIsekaiGame.Equipment
{
    [Serializable]
    public struct StatModifiers
    {
        [SerializeField] private float maximumHealth;
        [SerializeField] private float maximumStamina;
        [SerializeField] private float maximumMana;
        [SerializeField] private float attackPower;
        [SerializeField] private float defense;

        public float MaximumHealth => maximumHealth;
        public float MaximumStamina => maximumStamina;
        public float MaximumMana => maximumMana;
        public float AttackPower => attackPower;
        public float Defense => defense;

        public static StatModifiers operator +(StatModifiers left, StatModifiers right)
        {
            return new StatModifiers
            {
                maximumHealth = left.maximumHealth + right.maximumHealth,
                maximumStamina = left.maximumStamina + right.maximumStamina,
                maximumMana = left.maximumMana + right.maximumMana,
                attackPower = left.attackPower + right.attackPower,
                defense = left.defense + right.defense
            };
        }
    }
}
