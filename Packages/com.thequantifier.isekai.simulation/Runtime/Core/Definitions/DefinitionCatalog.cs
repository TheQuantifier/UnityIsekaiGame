using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.GameData
{
    [Serializable]
    public sealed class DefinitionCatalogSection
    {
        [SerializeField] private string domainId;
        [SerializeField] private ScriptableObject[] definitions = Array.Empty<ScriptableObject>();

        public string DomainId => domainId ?? string.Empty;
        public IReadOnlyList<ScriptableObject> Definitions => definitions ?? Array.Empty<ScriptableObject>();
    }

    [CreateAssetMenu(fileName = "DefinitionCatalog", menuName = "Unity Isekai Game/Game Data/Definition Catalog")]
    public sealed class DefinitionCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId = "catalog.prototype";
        [SerializeField] private string contentVersion = "phase-3.group-1";
        [SerializeField] private string contentHash;
        [SerializeField] private GameDataDefaultsDefinition defaults;
        [SerializeField] private DefinitionCatalogSection[] sections = Array.Empty<DefinitionCatalogSection>();

        public string CatalogId => catalogId;
        public string ContentVersion => contentVersion;
        public string ContentHash => contentHash ?? string.Empty;
        public GameDataDefaultsDefinition Defaults => defaults;
        public IReadOnlyList<DefinitionCatalogSection> Sections => sections ?? Array.Empty<DefinitionCatalogSection>();
        public IReadOnlyList<ScriptableObject> DefinitionAssets
        {
            get
            {
                List<ScriptableObject> assets = new List<ScriptableObject>();
                foreach (DefinitionCatalogSection section in Sections)
                {
                    if (section != null) assets.AddRange(section.Definitions);
                }
                return assets;
            }
        }

        public IEnumerable<IGameDefinition> GetDefinitions()
        {
            IReadOnlyList<ScriptableObject> assets = DefinitionAssets;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] is IGameDefinition definition)
                {
                    yield return definition;
                }
            }
        }

        public DefinitionRegistry CreateRegistry(DefinitionValidationReport report = null)
        {
            return new DefinitionRegistry(GetDefinitions(), report, defaults);
        }
    }
}
