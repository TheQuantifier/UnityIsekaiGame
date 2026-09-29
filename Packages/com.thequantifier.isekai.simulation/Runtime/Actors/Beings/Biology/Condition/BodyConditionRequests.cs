namespace UnityIsekaiGame.Beings.Biology.Condition
{
    public sealed class LocalizedStructuralDamageRequest
    {
        public string TransactionId { get; set; }
        public string SourceActorBodyId { get; set; }
        public string TargetActorBodyId { get; set; }
        public string TargetNodeId { get; set; }
        public string InjuryDefinitionId { get; set; }
        public string DamageTypeId { get; set; }
        public int StructuralDamage { get; set; }
        public long ExpectedBodyRevision { get; set; }
        public long ExpectedAnatomyRevision { get; set; }
        public bool AllowUnavailableTarget { get; set; }
        public string Context { get; set; }
    }

    public sealed class StructuralRecoveryRequest
    {
        public string TransactionId { get; set; }
        public string SourceId { get; set; }
        public string TargetActorBodyId { get; set; }
        public string TargetNodeId { get; set; }
        public string TargetInjuryId { get; set; }
        public string RecoveryMethodId { get; set; }
        public int IntegrityRestoration { get; set; }
        public int MaximumRecoverableIntegrity { get; set; }
        public bool ResolveInjuryOnCompletion { get; set; }
        public bool AllowDestroyedStructure { get; set; }
        public bool AllowMissingStructure { get; set; }
        public bool AllowSeveredStructure { get; set; }
        public long ExpectedConditionRevision { get; set; }
        public string Context { get; set; }
    }
}
