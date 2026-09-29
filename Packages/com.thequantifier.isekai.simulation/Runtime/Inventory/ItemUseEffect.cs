using UnityEngine;

namespace UnityIsekaiGame.Inventory
{
    public abstract class ItemUseEffect : ScriptableObject
    {
        public abstract bool CanUse(in ItemUseContext context, out string failureReason);
        public abstract void Apply(in ItemUseContext context);
    }
}
