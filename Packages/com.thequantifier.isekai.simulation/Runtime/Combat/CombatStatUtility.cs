using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Combat
{
    public static class CombatStatUtility
    {
        public static float GetAttackPower(GameObject actor)
        {
            return TryGetAttackPower(actor, out float attackPower) ? attackPower : 0f;
        }

        public static bool TryGetAttackPower(GameObject actor, out float attackPower)
        {
            if (TryGetStatValue(actor, CalculatedStatIds.PhysicalPower, out float value))
            {
                attackPower = Mathf.Max(0f, value);
                return true;
            }

            attackPower = 0f;
            return false;
        }

        public static float GetDefense(GameObject actor)
        {
            return TryGetDefense(actor, out float defense) ? defense : 0f;
        }

        public static bool TryGetDefense(GameObject actor, out float defense)
        {
            if (TryGetStatValue(actor, CalculatedStatIds.PhysicalDefense, out float value))
            {
                defense = Mathf.Max(0f, value);
                return true;
            }

            defense = 0f;
            return false;
        }

        public static float CalculatePreMitigationDamage(float baseDamage, GameObject source, AttackPowerScalingPolicy attackPowerScaling)
        {
            float damage = Mathf.Max(0f, baseDamage);
            if (attackPowerScaling == AttackPowerScalingPolicy.AddSourceAttackPower)
            {
                damage += GetAttackPower(source);
            }

            return damage;
        }

        private static bool TryGetStatValue(GameObject actor, string statId, out float value)
        {
            value = 0f;
            if (actor == null)
            {
                return false;
            }

            IRuntimeCalculatedStatReceiver receiver = actor.GetComponentInParent<IRuntimeCalculatedStatReceiver>();
            if (receiver == null || !receiver.HasCalculatedStat(statId))
            {
                receiver = actor.GetComponentInChildren<IRuntimeCalculatedStatReceiver>();
            }

            if (receiver == null || !receiver.HasCalculatedStat(statId))
            {
                return false;
            }

            value = receiver.GetCalculatedStatValue(statId);
            return true;
        }
    }
}
