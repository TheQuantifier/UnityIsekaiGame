using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Inventory.Quality
{
    public interface IItemQualityAffixRuntimeProvider
    {
        ItemInstanceIdentityRuntime ItemIdentities { get; }
        ItemCompositionRuntime ItemCompositions { get; }
        ItemQualityAffixRuntime ItemQualityAffixes { get; }
        DefinitionRegistry ItemQualityDefinitionRegistry { get; }
    }
}
