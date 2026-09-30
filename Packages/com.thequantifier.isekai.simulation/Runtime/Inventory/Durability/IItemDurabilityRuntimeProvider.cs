using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Inventory.Durability
{
    public interface IItemDurabilityRuntimeProvider : IItemQualityAffixRuntimeProvider
    {
        ItemDurabilityRuntime ItemDurability { get; }
        DefinitionRegistry ItemDurabilityDefinitionRegistry { get; }
    }
}
