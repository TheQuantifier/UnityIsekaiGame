namespace UnityIsekaiGame.Knowledge.Records
{
    public enum KnowledgeRecordCategory
    {
        Unknown = 0,
        PersonalJournal = 10,
        KnowledgeJournal = 20,
        HistoricalRecord = 30,
        Biography = 40,
        PersonalTimeline = 45,
        PublicBiography = 50,
        Bestiary = 60,
        SpeciesRecord = 70,
        CreatureRecord = 80,
        PersonRecord = 90,
        OrganizationRecord = 100,
        FactionRecord = 110,
        LocationRecord = 120,
        SettlementRecord = 130,
        RegionRecord = 140,
        MapRecord = 150,
        DiscoveryRecord = 160,
        MedicalRecord = 170,
        DiagnosisRecord = 180,
        InvestigationRecord = 190,
        EvidenceRecord = 200,
        SourceRecord = 210,
        EventRecord = 220,
        LifeEventRecord = 230,
        QuestRelatedRecord = 240,
        ItemRecord = 250,
        MaterialRecord = 260,
        ProcedureRecord = 270,
        SkillConceptRecord = 280,
        RecipeDiscoveryRecord = 290,
        LegalRecord = 300,
        Collection = 310,
        Custom = 1000
    }

    public enum KnowledgeRecordOwnerKind
    {
        Unknown = 0,
        Person = 10,
        Organization = 20,
        Institution = 30,
        Government = 40,
        Guild = 50,
        Healer = 60,
        Scholar = 70,
        QuestSystem = 80,
        PublicWorldRecord = 90,
        PrivateJournal = 100,
        SharedArchive = 110,
        Debug = 900,
        Custom = 1000
    }

    public enum KnowledgeRecordProjectionKind
    {
        Unknown = 0,
        ExplicitRecord = 10,
        LiveProjection = 20,
        Hybrid = 30
    }

    public enum KnowledgeRecordPersistencePolicy
    {
        Unknown = 0,
        ExplicitOnly = 10,
        LiveOnly = 20,
        HybridExplicitStateOnly = 30
    }

    public enum KnowledgeRecordStatus
    {
        Unknown = 0,
        Draft = 10,
        Active = 20,
        Incomplete = 30,
        Unverified = 40,
        Verified = 50,
        Disputed = 60,
        Corrected = 70,
        Superseded = 80,
        Retracted = 90,
        Outdated = 100,
        Archived = 110,
        Hidden = 120,
        Sealed = 130,
        DestroyedRecordReference = 140,
        Lost = 150,
        Recovered = 160,
        Custom = 1000
    }

    public enum KnowledgeRecordProjectionContextKind
    {
        Unknown = 0,
        AuthoritativeDebug = 10,
        Public = 20,
        Owner = 30,
        PersonKnown = 40,
        PersonBelieved = 50,
        PersonRemembered = 60,
        PersonRecorded = 70,
        OrganizationRecord = 80,
        Medical = 90,
        Investigation = 100,
        SourceProtected = 110,
        Redacted = 120,
        Privileged = 130,
        FuturePlayerJournal = 140,
        FutureCodex = 150,
        Custom = 1000
    }

    public enum KnowledgeRecordCompleteness
    {
        Unknown = 0,
        Empty = 10,
        Partial = 20,
        Substantial = 30,
        Complete = 40
    }

    public enum KnowledgeRecordResultCode
    {
        Success = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingDefinition = 20,
        MissingRecord = 30,
        MissingCollection = 40,
        AccessDenied = 50,
        Redacted = 60,
        ImmutableProjection = 70,
        DuplicateMembership = 80,
        CircularCorrection = 90,
        CorruptPayload = 100,
        RestoreFailed = 110,
        PartialMutationRejected = 120
    }
}
