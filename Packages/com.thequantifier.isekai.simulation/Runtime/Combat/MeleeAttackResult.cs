using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public readonly struct MeleeAttackResult
    {
        public MeleeAttackResult(
            bool started,
            bool hitTarget,
            string attackName,
            float damageAmount,
            GameObject target,
            DamageResult damageResult,
            string message)
        {
            Started = started;
            HitTarget = hitTarget;
            AttackName = attackName;
            DamageAmount = damageAmount;
            Target = target;
            DamageResult = damageResult;
            Message = message;
        }

        public bool Started { get; }
        public bool HitTarget { get; }
        public string AttackName { get; }
        public float DamageAmount { get; }
        public GameObject Target { get; }
        public DamageResult DamageResult { get; }
        public string Message { get; }

        public static MeleeAttackResult Failure(string message)
        {
            return new MeleeAttackResult(false, false, string.Empty, 0f, null, default, message);
        }

        public static MeleeAttackResult Miss(string attackName, float damageAmount, string message)
        {
            return new MeleeAttackResult(true, false, attackName, damageAmount, null, default, message);
        }

        public static MeleeAttackResult Hit(string attackName, float damageAmount, GameObject target, DamageResult damageResult, string message)
        {
            return new MeleeAttackResult(true, true, attackName, damageAmount, target, damageResult, message);
        }
    }
}
