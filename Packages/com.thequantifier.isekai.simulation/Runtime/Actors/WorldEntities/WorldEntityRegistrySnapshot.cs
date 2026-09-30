namespace UnityIsekaiGame.WorldEntities
{
    public readonly struct WorldEntityRegistrySnapshot
    {
        public WorldEntityRegistrySnapshot(string entityId, string sceneKey, string worldId, WorldEntityIdentityKind identityKind, bool activeInHierarchy, bool persistenceEligible, string definitionId, string expectedEntityType)
        {
            EntityId = entityId;
            SceneKey = sceneKey;
            WorldId = worldId;
            IdentityKind = identityKind;
            ActiveInHierarchy = activeInHierarchy;
            PersistenceEligible = persistenceEligible;
            DefinitionId = definitionId;
            ExpectedEntityType = expectedEntityType;
        }

        public string EntityId { get; }
        public string SceneKey { get; }
        public string WorldId { get; }
        public WorldEntityIdentityKind IdentityKind { get; }
        public bool ActiveInHierarchy { get; }
        public bool PersistenceEligible { get; }
        public string DefinitionId { get; }
        public string ExpectedEntityType { get; }
    }
}
