namespace UnityIsekaiGame.Factions
{
    public enum FactionRestoreOrder
    {
        ResolveFactionDefinitions = 10,
        RestoreMutableFactionState = 20,
        RestoreLeadershipChanges = 30,
        RestoreMembershipsAndRanks = 40,
        RestoreReputationAndLegalStanding = 50,
        RestoreDiplomacyAndRelationships = 60,
        RestoreFactionOwnedWorldState = 70,
        NotifyDependentSystems = 80
    }
}
