using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class PrototypeEnemyAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyHealth health;
        [SerializeField] private EnemyMeleeAttack meleeAttack;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string defeatedParameter = "IsDefeated";
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;

        private Vector3 previousPosition;
        private int speedHash;
        private int attackHash;
        private int defeatedHash;

        private void Awake()
        {
            ResolveReferences();
            CacheHashes();
            previousPosition = transform.position;
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheHashes();
            previousPosition = transform.position;

            if (meleeAttack != null)
            {
                meleeAttack.AttackResolved += OnAttackResolved;
            }

            if (health != null)
            {
                health.Defeated += OnDefeated;
            }
        }

        private void OnDisable()
        {
            if (meleeAttack != null)
            {
                meleeAttack.AttackResolved -= OnAttackResolved;
            }

            if (health != null)
            {
                health.Defeated -= OnDefeated;
            }
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 planarDelta = currentPosition - previousPosition;
            planarDelta.y = 0f;
            float planarSpeed = Time.deltaTime <= 0f ? 0f : planarDelta.magnitude / Time.deltaTime;
            previousPosition = currentPosition;

            if (HasParameter(speedHash))
            {
                animator.SetFloat(speedHash, planarSpeed, speedDampTime, Time.deltaTime);
            }

            if (HasParameter(defeatedHash))
            {
                animator.SetBool(defeatedHash, health != null && health.IsDefeated);
            }
        }

        private void OnValidate()
        {
            speedDampTime = Mathf.Max(0f, speedDampTime);
        }

        private void OnAttackResolved(DamageResult result)
        {
            if (animator == null || !result.Applied || !HasParameter(attackHash))
            {
                return;
            }

            animator.ResetTrigger(attackHash);
            animator.SetTrigger(attackHash);
        }

        private void OnDefeated()
        {
            if (animator != null && HasParameter(defeatedHash))
            {
                animator.SetBool(defeatedHash, true);
            }
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (meleeAttack == null)
            {
                meleeAttack = GetComponent<EnemyMeleeAttack>();
            }
        }

        private void CacheHashes()
        {
            speedHash = string.IsNullOrWhiteSpace(speedParameter) ? 0 : Animator.StringToHash(speedParameter);
            attackHash = string.IsNullOrWhiteSpace(attackTrigger) ? 0 : Animator.StringToHash(attackTrigger);
            defeatedHash = string.IsNullOrWhiteSpace(defeatedParameter) ? 0 : Animator.StringToHash(defeatedParameter);
        }

        private bool HasParameter(int parameterHash)
        {
            if (animator == null || parameterHash == 0)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
