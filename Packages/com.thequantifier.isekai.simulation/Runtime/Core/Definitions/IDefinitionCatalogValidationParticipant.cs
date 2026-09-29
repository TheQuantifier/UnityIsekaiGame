using System.Collections.Generic;

namespace UnityIsekaiGame.GameData
{
    public interface IDefinitionCatalogValidationParticipant
    {
        void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report);
    }
}
