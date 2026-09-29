using System.Collections.Generic;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Combat
{
    public interface IDamageResistanceReceiver
    {
        event System.Action<DamageTypeDefinition, float> ResistanceChanged;
        float GetDirectResistance(DamageTypeDefinition damageType);
        float GetEffectiveResistance(DamageTypeDefinition damageType);
        bool AddResistanceModifier(RuntimeResistanceModifier modifier);
        bool RemoveResistanceModifiersFromSource(StatModifierSource source);
        IReadOnlyList<RuntimeResistanceModifier> GetResistanceModifiers();
    }
}
