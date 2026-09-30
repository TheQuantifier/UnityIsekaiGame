using System;
using UnityEngine;

namespace UnityIsekaiGame.Stats
{
    [Serializable]
    public sealed class AttributeFormulaTerm
    {
        [SerializeField] private AttributeDefinition attribute;
        [SerializeField] private float weight = 1f;

        public AttributeDefinition Attribute => attribute;
        public float Weight => IsFinite(weight) ? weight : 0f;
        public bool IsValid => attribute != null && !string.IsNullOrWhiteSpace(attribute.Id) && IsFinite(weight);

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
