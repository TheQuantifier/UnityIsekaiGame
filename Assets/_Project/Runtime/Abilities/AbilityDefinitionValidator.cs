using System.Collections.Generic;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Abilities
{
    public static class AbilityDefinitionValidator
    {
        public static void ValidateAbility(AbilityDefinition ability, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (ability == null || report == null)
            {
                return;
            }

            if (ability.PrimaryCategory == null)
            {
                report.AddError($"Ability '{ability.DisplayName}' is missing a primary category.");
            }

            if (ability.Range < 0f)
            {
                report.AddError($"Ability '{ability.DisplayName}' has negative range.");
            }

            if (ability.Execution == null)
            {
                report.AddError($"Ability '{ability.DisplayName}' is missing its combat execution definition.");
            }
            else if (definitionsById != null &&
                (!definitionsById.TryGetValue(ability.Execution.Id, out IGameDefinition execution) || !ReferenceEquals(execution, ability.Execution)))
            {
                report.AddError($"Ability '{ability.DisplayName}' references execution '{ability.Execution.Id}', which is not in the configured catalog.");
            }

            if (!System.Enum.IsDefined(typeof(AbilityTargetingMode), ability.TargetingMode))
            {
                report.AddError($"Ability '{ability.DisplayName}' has invalid targeting mode.");
            }

            if (!System.Enum.IsDefined(typeof(AbilityDeliveryMode), ability.DeliveryMode))
            {
                report.AddError($"Ability '{ability.DisplayName}' has invalid delivery mode.");
            }

            ValidateEffects(ability, definitionsById, report);
            ValidateDelivery(ability, report);
        }

        public static void ValidateEffect(EffectDefinition effect, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            effect?.ValidateDefinition(report);
        }

        private static void ValidateEffects(AbilityDefinition ability, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (ability.Effects.Count == 0)
            {
                report.AddError($"Ability '{ability.DisplayName}' has no effects.");
                return;
            }

            for (int i = 0; i < ability.Effects.Count; i++)
            {
                EffectDefinition effect = ability.Effects[i];
                if (effect == null)
                {
                    report.AddError($"Ability '{ability.DisplayName}' has a missing effect reference at index {i}.");
                    continue;
                }

                if (definitionsById != null
                    && (!definitionsById.TryGetValue(effect.Id, out IGameDefinition found) || !ReferenceEquals(found, effect)))
                {
                    report.AddError($"Ability '{ability.DisplayName}' references effect '{effect.Id}', which is not in the configured catalog.");
                }
            }
        }

        private static void ValidateDelivery(AbilityDefinition ability, DefinitionValidationReport report)
        {
            if (ability.DeliveryMode == AbilityDeliveryMode.Projectile)
            {
                if (ability.ProjectileDelivery == null || ability.ProjectileDelivery.ProjectilePrefab == null)
                {
                    report.AddError($"Ability '{ability.DisplayName}' uses projectile delivery without a projectile prefab.");
                }

                return;
            }

            if (ability.DeliveryMode == AbilityDeliveryMode.Immediate && ability.ProjectileDelivery != null && ability.ProjectileDelivery.ProjectilePrefab != null)
            {
                report.AddWarning($"Ability '{ability.DisplayName}' uses immediate delivery but has projectile configuration.");
            }

            if (ability.TargetingMode == AbilityTargetingMode.Direction && ability.DeliveryMode == AbilityDeliveryMode.Immediate)
            {
                report.AddWarning($"Ability '{ability.DisplayName}' targets a direction but uses immediate delivery.");
            }
        }

        public static void ValidateSpellAdapter(UnityIsekaiGame.Magic.SpellDefinition spell, DefinitionValidationReport report)
        {
            if (spell == null || report == null)
            {
                return;
            }
            if (spell.Ability == null)
            {
                report.AddError($"Spell '{spell.DisplayName}' is missing its ability definition.");
            }
        }
    }
}
