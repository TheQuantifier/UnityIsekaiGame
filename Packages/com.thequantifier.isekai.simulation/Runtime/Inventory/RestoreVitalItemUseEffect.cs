using UnityEngine;
using UnityIsekaiGame.Abilities;

namespace UnityIsekaiGame.Inventory
{
    [CreateAssetMenu(fileName = "RestoreVitalItemUseEffect", menuName = "Unity Isekai Game/Inventory/Effects/Restore Vital")]
    public sealed class RestoreVitalItemUseEffect : ItemUseEffect
    {
        [SerializeField] private RestoreVitalEffectDefinition restoreEffect;
        [SerializeField, Min(1)] private int restoreAmount = 25;

        public RestoreVitalEffectDefinition RestoreEffect => restoreEffect;
        public int RestoreAmount => restoreAmount;

        private void OnValidate()
        {
            restoreAmount = Mathf.Max(1, restoreAmount);
        }

        public override bool CanUse(in ItemUseContext context, out string failureReason)
        {
            if (restoreEffect == null)
            {
                failureReason = "No restore effect is configured.";
                return false;
            }

            EffectExecutionResult result = restoreEffect.CanExecute(CreateEffectContext(context));
            failureReason = result.Succeeded ? string.Empty : result.Message;
            return result.Succeeded;
        }

        public override void Apply(in ItemUseContext context)
        {
            if (restoreEffect == null)
            {
                return;
            }

            EffectExecutionResult result = restoreEffect.Execute(CreateEffectContext(context));
            Debug.Log(result.Message);
        }

        private static EffectExecutionContext CreateEffectContext(in ItemUseContext context)
        {
            Vector3 position = context.User == null ? Vector3.zero : context.User.transform.position;
            return new EffectExecutionContext(
                null,
                context.User,
                context.User,
                position,
                position,
                context.User == null ? Vector3.forward : context.User.transform.forward,
                context.Item);
        }
    }
}
