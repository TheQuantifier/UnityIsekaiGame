using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Integration;
using UnityIsekaiGame.Knowledge.Observation;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Rumors;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.WorldEntities;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Gameplay
{
    public sealed partial class PrototypePersistenceServiceBehaviour : MonoBehaviour, IItemDurabilityRuntimeProvider
    {
        [SerializeField] private DefinitionCatalog definitionCatalog;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerMana playerMana;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField] private CharacterAttributes playerAttributes;
        [SerializeField] private CalculatedStatCollection playerCalculatedStats;
        [SerializeField] private CharacterResourceCollection playerResources;
        [SerializeField] private ActorLifecycleController playerActorLifecycle;
        [SerializeField] private OngoingEffectService playerOngoingEffects;
        [SerializeField] private CharacterSkillCollection playerSkills;
        [SerializeField] private CharacterTraitCollection playerTraits;
        [SerializeField] private ActorBodyRuntime playerBody;
        [SerializeField] private PersonKnowledgeRuntime playerKnowledge;
        [SerializeField] private PlayerSkillActionEventSource playerSkillActionEventSource;
        [SerializeField] private StatusEffectController statusEffectController;
        [SerializeField] private PlayerIdentityProgression playerIdentityProgression;
        [SerializeField] private OverallLevelConfiguration overallLevelConfiguration;
        [SerializeField] private PlayerQuestLog playerQuestLog;
        [SerializeField] private PlayerContractJournal playerContractJournal;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private PlayerInputReader playerInput;
        [SerializeField] private MonoBehaviour inventoryScreenController;
        [SerializeField] private CurrentPlaceTracker currentPlaceTracker;
        [SerializeField] private string sceneKey = "scene.prototype";
        [SerializeField] private string defaultSpawnPointId = "spawn.prototype.default";
        [SerializeField] private string defaultPlayerSpeciesId = "species.human";
        [SerializeField] private bool registerPlayerInventoryEquipment = true;
        [SerializeField] private bool registerPlayerItemIdentities = true;
        [SerializeField] private bool registerWorldEconomy = true;
        [SerializeField] private bool registerWorldMarkets = true;
        [SerializeField] private bool registerWorldTrades = true;
        [SerializeField] private bool registerWorldPayroll = true;
        [SerializeField] private bool registerWorldBusinesses = true;
        [SerializeField] private bool registerWorldProperties = true;
        [SerializeField] private bool registerWorldContracts = true;
        [SerializeField] private bool registerWorldInstitutionalRevenue = true;
        [SerializeField] private bool registerWorldRegionalFlow = true;
        [SerializeField] private bool registerWorldOrganizations = true;
        [SerializeField] private bool registerWorldOrganizationMemberships = true;
        [SerializeField] private bool registerWorldOrganizationAuthority = true;
        [SerializeField] private bool registerWorldOrganizationResources = true;
        [SerializeField] private bool registerWorldOrganizationDecisions = true;
        [SerializeField] private bool registerWorldFactions = true;
        [SerializeField] private bool registerWorldDiplomacy = true;
        [SerializeField] private bool registerWorldGovernments = true;
        [SerializeField] private bool registerWorldLaws = true;
        [SerializeField] private bool registerWorldCrimes = true;
        [SerializeField] private bool registerWorldJustice = true;
        [SerializeField] private bool registerPlayerItemCompositions = true;
        [SerializeField] private bool registerPlayerItemQualityAffixes = true;
        [SerializeField] private bool registerPlayerItemDurability = true;
        [SerializeField] private bool registerPlayerProductionRequirements = true;
        [SerializeField] private bool registerPlayerRecipeKnowledge = true;
        [SerializeField] private bool registerPlayerCraftingExecution = true;
        [SerializeField] private bool registerPlayerProductionWorkflow = true;
        [SerializeField] private bool registerPlayerExperimentation = true;
        [SerializeField] private bool registerPlayerIdentityProgression = true;
        [SerializeField] private bool registerPlayerAttributes = true;
        [SerializeField] private bool registerPlayerSkills = true;
        [SerializeField] private bool registerPlayerTraits = true;
        [SerializeField] private bool registerPlayerBody = true;
        [SerializeField] private bool registerPlayerKnowledge = true;
        [SerializeField] private bool registerWorldAuthoritativeHistory = true;
        [SerializeField] private bool registerPlayerMemory = true;
        [SerializeField] private bool registerPlayerProfessions = true;
        [SerializeField] private bool registerPlayerProfessionEntries = true;
        [SerializeField] private bool registerPlayerTraining = true;
        [SerializeField] private bool registerPlayerProfessionalActivities = true;
        [SerializeField] private bool registerPlayerCredentials = true;
        [SerializeField] private bool registerPlayerProfessionalRanks = true;
        [SerializeField] private bool registerPlayerPositionEmployment = true;
        [SerializeField] private bool registerPlayerCareerHistory = true;
        [SerializeField] private bool registerPlayerLifePaths = true;
        [SerializeField] private bool registerPlayerRelationships = true;
        [SerializeField] private bool registerPlayerInterpersonalAttitudes = true;
        [SerializeField] private bool registerWorldReputation = true;
        [SerializeField] private bool registerWorldRumors = true;
        [SerializeField] private bool registerWorldSocialInteractions = true;
        [SerializeField] private bool registerWorldSocialNorms = true;
        [SerializeField] private bool registerWorldSocialNetworks = true;
        [SerializeField] private bool registerWorldSocialDecisions = true;
        [SerializeField] private bool registerWorldSocialInfluence = true;
        [SerializeField] private bool registerWorldSocialEmotions = true;
        [SerializeField] private bool registerWorldFamilyRelationships = true;
        [SerializeField] private bool registerWorldInformationSources = true;
        [SerializeField] private bool registerWorldInformationTransfers = true;
        [SerializeField] private bool registerWorldInformationAccess = true;
        [SerializeField] private bool registerWorldKnowledgeRecords = true;
        [SerializeField] private bool registerPlayerStatusEffects = true;
        [SerializeField] private bool registerPlayerResources = true;
        [SerializeField] private bool registerPlayerCombatExecution = true;
        [SerializeField] private bool registerPlayerActorLifecycle = true;
        [SerializeField] private bool registerPlayerOngoingEffects = true;
        [SerializeField] private bool registerPlayerQuestContract = true;
        [SerializeField] private bool registerPlayerLocation = true;
        [Header("Save Slots")]
        [SerializeField, Min(1)] private int manualSlotCount = PrototypeSaveSlotCatalog.DefaultManualSlotCount;
        [SerializeField, Min(1)] private int autosaveSlotCount = PrototypeSaveSlotCatalog.DefaultAutosaveSlotCount;
        [SerializeField, Min(5f)] private float autosaveIntervalSeconds = 300f;
        [SerializeField] private PlayTimeTracker playTimeTracker;
        [SerializeField] private GameSaveDirtyTracker dirtyTracker;
        [SerializeField] private AutosaveCoordinator autosaveCoordinator;

        private PersistenceService playerService;
        private PersistenceService worldService;
        private PlayerPersistenceContext playerPersistenceContext;
        private WorldPersistenceContext worldPersistenceContext;
        private PlayerIdentityProgressionPersistenceParticipant identityProgressionParticipant;
        private PlayerAttributesPersistenceParticipant playerAttributesParticipant;
        private PlayerSkillsPersistenceParticipant playerSkillsParticipant;
        private PlayerTraitsPersistenceParticipant playerTraitsParticipant;
        private PlayerBodyPersistenceParticipant playerBodyParticipant;
        private PersonKnowledgePersistenceParticipant playerKnowledgeParticipant;
        private AuthoritativeHistoryPersistenceParticipant worldAuthoritativeHistoryParticipant;
        private PersonMemoryPersistenceParticipant playerMemoryParticipant;
        private InformationSourcePersistenceParticipant playerInformationSourceParticipant;
        private InformationTransferPersistenceParticipant playerInformationTransferParticipant;
        private InformationAccessPersistenceParticipant playerInformationAccessParticipant;
        private KnowledgeRecordPersistenceParticipant playerKnowledgeRecordParticipant;
        private PersonProfessionPersistenceParticipant playerProfessionParticipant;
        private ProfessionEntryPersistenceParticipant playerProfessionEntryParticipant;
        private TrainingPersistenceParticipant playerTrainingParticipant;
        private ProfessionalActivityPersistenceParticipant playerProfessionalActivityParticipant;
        private CredentialPersistenceParticipant playerCredentialParticipant;
        private ProfessionalRankPersistenceParticipant playerProfessionalRankParticipant;
        private PositionEmploymentPersistenceParticipant playerPositionEmploymentParticipant;
        private CareerHistoryPersistenceParticipant playerCareerHistoryParticipant;
        private LifePathPersistenceParticipant playerLifePathParticipant;
        private RelationshipPersistenceParticipant playerRelationshipParticipant;
        private InterpersonalAttitudePersistenceParticipant playerInterpersonalAttitudeParticipant;
        private ReputationPersistenceParticipant worldReputationParticipant;
        private RumorPersistenceParticipant worldRumorParticipant;
        private SocialInteractionPersistenceParticipant worldSocialInteractionParticipant;
        private SocialNormPersistenceParticipant worldSocialNormParticipant;
        private SocialNetworkPersistenceParticipant worldSocialNetworkParticipant;
        private SocialDecisionPersistenceParticipant worldSocialDecisionParticipant;
        private SocialInfluencePersistenceParticipant worldSocialInfluenceParticipant;
        private SocialEmotionPersistenceParticipant worldSocialEmotionParticipant;
        private FamilyRelationshipPersistenceParticipant worldFamilyRelationshipParticipant;
        private PlayerInventoryEquipmentPersistenceParticipant inventoryEquipmentParticipant;
        private ItemInstanceIdentityPersistenceParticipant itemIdentityParticipant;
        private EconomyPersistenceParticipant economyParticipant;
        private MarketPersistenceParticipant marketParticipant;
        private TradePersistenceParticipant tradeParticipant;
        private PayrollPersistenceParticipant payrollParticipant;
        private BusinessPersistenceParticipant businessParticipant;
        private PropertyPersistenceParticipant propertyParticipant;
        private ContractEconomyPersistenceParticipant contractEconomyParticipant;
        private InstitutionalRevenuePersistenceParticipant institutionalRevenueParticipant;
        private RegionalFlowPersistenceParticipant regionalFlowParticipant;
        private OrganizationPersistenceParticipant organizationParticipant;
        private OrganizationMembershipPersistenceParticipant organizationMembershipParticipant;
        private OrganizationAuthorityPersistenceParticipant organizationAuthorityParticipant;
        private OrganizationResourcePersistenceParticipant organizationResourceParticipant;
        private OrganizationDecisionPersistenceParticipant organizationDecisionParticipant;
        private FactionPersistenceParticipant factionParticipant;
        private DiplomacyPersistenceParticipant diplomacyParticipant;
        private GovernmentPersistenceParticipant governmentParticipant;
        private LegalPersistenceParticipant legalParticipant;
        private CrimePersistenceParticipant crimeParticipant;
        private JusticePersistenceParticipant justiceParticipant;
        private ItemCompositionPersistenceParticipant itemCompositionParticipant;
        private ItemQualityAffixPersistenceParticipant itemQualityAffixParticipant;
        private ItemDurabilityPersistenceParticipant itemDurabilityParticipant;
        private ProductionRequirementPersistenceParticipant productionRequirementParticipant;
        private RecipeKnowledgePersistenceParticipant recipeKnowledgeParticipant;
        private CraftingExecutionPersistenceParticipant craftingExecutionParticipant;
        private ProductionWorkflowPersistenceParticipant productionWorkflowParticipant;
        private ExperimentationPersistenceParticipant experimentationParticipant;
        private PlayerStatusEffectsPersistenceParticipant statusEffectsParticipant;
        private PlayerResourcesPersistenceParticipant playerResourcesParticipant;
        private PlayerCombatExecutionPersistenceParticipant playerCombatExecutionParticipant;
        private PlayerActorLifecyclePersistenceParticipant playerActorLifecycleParticipant;
        private PlayerOngoingEffectsPersistenceParticipant playerOngoingEffectsParticipant;
        private PlayerQuestContractPersistenceParticipant questContractParticipant;
        private PlayerLocationPersistenceParticipant playerLocationParticipant;
        private DefinitionRegistry definitionRegistry;
        private bool sceneCharactersInitialized;
        private CombatExecutionService combatExecutionService;
        private AttackResolutionService attackResolutionService;
        private InformationSourceRuntime playerInformationSources;
        private InformationTransferRuntime playerInformationTransfers;
        private InformationAccessRuntime playerInformationAccess;
        private KnowledgeRecordRuntime playerKnowledgeRecords;
        private AuthoritativeHistoryRuntime worldAuthoritativeHistory;
        private PersonMemoryRuntime playerMemory;
        private KnowledgeHistoryFacade knowledgeHistoryFacade;
        private ObservationService observationService;
        private GameplayObservationCoordinator gameplayObservationCoordinator;
        private MemoryMaintenanceService memoryMaintenance;
        private KnowledgeHistoryEventBridge knowledgeHistoryEventBridge;
        private bool characterCreationHistoryEnsured;
        private PersonProfessionRuntime playerProfessions;
        private ProfessionEntryRuntime playerProfessionEntries;
        private TrainingRuntime playerTraining;
        private ProfessionalActivityRuntime playerProfessionalActivities;
        private CredentialRuntime playerCredentials;
        private ProfessionalRankRuntime playerProfessionalRanks;
        private PositionEmploymentRuntime playerPositionEmployment;
        private CareerHistoryRuntime playerCareerHistory;
        private LifePathRuntime playerLifePaths;
        private RelationshipRuntime playerRelationships;
        private InterpersonalAttitudeRuntime playerInterpersonalAttitudes;
        private ReputationRuntime worldReputation;
        private RumorRuntime worldRumors;
        private SocialInteractionRuntime worldSocialInteractions;
        private SocialNormRuntime worldSocialNorms;
        private SocialNetworkRuntime worldSocialNetworks;
        private SocialDecisionRuntime worldSocialDecisions;
        private SocialInfluenceRuntime worldSocialInfluence;
        private SocialEmotionRuntime worldSocialEmotions;
        private FamilyRelationshipRuntime worldFamilyRelationships;
        private ItemInstanceIdentityRuntime playerItemIdentities;
        private EconomyRuntime worldEconomy;
        private MarketRuntime worldMarkets;
        private TradeRuntime worldTrades;
        private PayrollRuntime worldPayroll;
        private BusinessRuntime worldBusinesses;
        private PropertyRuntime worldProperties;
        private ContractEconomyRuntime worldContracts;
        private InstitutionalRevenueRuntime worldInstitutionalRevenue;
        private RegionalFlowRuntime worldRegionalFlow;
        private OrganizationRuntime worldOrganizations;
        private OrganizationMembershipRuntime worldOrganizationMemberships;
        private OrganizationAuthorityRuntime worldOrganizationAuthority;
        private OrganizationResourceRuntime worldOrganizationResources;
        private OrganizationDecisionRuntime worldOrganizationDecisions;
        private FactionRuntime worldFactions;
        private DiplomacyRuntime worldDiplomacy;
        private GovernmentRuntime worldGovernments;
        private LegalRuntime worldLaws;
        private CrimeRuntime worldCrimes;
        private JusticeRuntime worldJustice;
        private ItemCompositionRuntime playerItemCompositions;
        private ItemQualityAffixRuntime playerItemQualityAffixes;
        private ItemDurabilityRuntime playerItemDurability;
        private ProductionRequirementRuntime playerProductionRequirements;
        private RecipeKnowledgeRuntime playerRecipeKnowledge;
        private CraftingExecutionRuntime playerCraftingExecution;
        private ProductionWorkflowRuntime playerProductionWorkflow;
        private ExperimentationRuntime playerExperimentation;
        private PlayerItemIdentitySynchronizer playerItemIdentitySynchronizer;
        private bool dirtyEventsSubscribed;

        public PersistenceService PlayerService => playerService;
        public PersistenceService WorldService => worldService;
        public PersistenceReadinessReport PlayerReadiness { get; private set; }
        public PersistenceReadinessReport WorldReadiness { get; private set; }
        public int ManualSlotCount => Mathf.Max(1, manualSlotCount);
        public int AutosaveSlotCount => Mathf.Max(1, autosaveSlotCount);
        public PlayTimeTracker PlayTime => playTimeTracker;
        public GameSaveDirtyTracker DirtyTracker => dirtyTracker;
        public AutosaveCoordinator Autosave => autosaveCoordinator;
        public DefinitionCatalog DefinitionCatalog => definitionCatalog;
        public CombatExecutionService CombatExecution => combatExecutionService ??= CreateCombatExecutionService();
        public AttackResolutionService AttackResolution
        {
            get
            {
                _ = CombatExecution;
                return attackResolutionService;
            }
        }

        private CombatExecutionService CreateCombatExecutionService()
        {
            DefensiveActionService defense = new DefensiveActionService();
            attackResolutionService = new AttackResolutionService(new DamageHealingService(), defense);
            return new CombatExecutionService(new ICombatExecutionHandler[]
            {
                new AbilityCombatExecutionHandler(),
                new AttackCombatExecutionHandler(attackResolutionService),
                new DefenseActivationCombatExecutionHandler(defense)
            });
        }
        public InformationSourceRuntime InformationSources => playerInformationSources ??= new InformationSourceRuntime();
        public InformationTransferRuntime InformationTransfers => playerInformationTransfers ??= new InformationTransferRuntime();
        public InformationAccessRuntime InformationAccess => playerInformationAccess ??= new InformationAccessRuntime();
        public KnowledgeRecordRuntime KnowledgeRecords => playerKnowledgeRecords ??= new KnowledgeRecordRuntime();
        public AuthoritativeHistoryRuntime AuthoritativeHistory => worldAuthoritativeHistory ??= new AuthoritativeHistoryRuntime();
        public PersonMemoryRuntime PlayerMemory => playerMemory ??= new PersonMemoryRuntime();
        public ObservationService Observations => observationService ??= new ObservationService(GetDefinitionRegistry());
        public GameplayObservationCoordinator GameplayObservations => gameplayObservationCoordinator ??= new GameplayObservationCoordinator(GetDefinitionRegistry(), Observations);
        public KnowledgeHistoryFacade KnowledgeHistory
        {
            get
            {
                EnsureKnowledgeHistoryRuntimesConfigured();
                return knowledgeHistoryFacade ??= new KnowledgeHistoryFacade(CreateKnowledgeHistoryRuntimeSet());
            }
        }
        public RelationshipRuntime Relationships
        {
            get
            {
                if (playerRelationships == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    playerRelationships = new RelationshipRuntime();
                    playerRelationships.Configure(GetDefinitionRegistry(), new[] { personId, playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId });
                }

                return playerRelationships;
            }
        }
        public InterpersonalAttitudeRuntime InterpersonalAttitudes
        {
            get
            {
                if (playerInterpersonalAttitudes == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    playerInterpersonalAttitudes = new InterpersonalAttitudeRuntime();
                    playerInterpersonalAttitudes.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId));
                }

                return playerInterpersonalAttitudes;
            }
        }
        public ReputationRuntime Reputation
        {
            get
            {
                if (worldReputation == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldReputation = new ReputationRuntime();
                    worldReputation.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId));
                }

                return worldReputation;
            }
        }
        public RumorRuntime Rumors
        {
            get
            {
                if (worldRumors == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldRumors = new RumorRuntime();
                    worldRumors.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), ResolveKnowledgeRuntimeForPerson, ResolveMemoryRuntimeForPerson);
                }

                return worldRumors;
            }
        }
        public SocialInteractionRuntime SocialInteractions
        {
            get
            {
                if (worldSocialInteractions == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldSocialInteractions = new SocialInteractionRuntime();
                    worldSocialInteractions.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), Relationships, InterpersonalAttitudes, Reputation, Rumors);
                }

                return worldSocialInteractions;
            }
        }
        public SocialNormRuntime SocialNorms
        {
            get
            {
                if (worldSocialNorms == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldSocialNorms = new SocialNormRuntime();
                    worldSocialNorms.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions);
                }

                return worldSocialNorms;
            }
        }
        public SocialNetworkRuntime SocialNetworks
        {
            get
            {
                if (worldSocialNetworks == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldSocialNetworks = new SocialNetworkRuntime();
                    worldSocialNetworks.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions, SocialNorms);
                }

                return worldSocialNetworks;
            }
        }

        public SocialDecisionRuntime SocialDecisions
        {
            get
            {
                if (worldSocialDecisions == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldSocialDecisions = new SocialDecisionRuntime();
                    worldSocialDecisions.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), SocialInteractions, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialNorms, SocialNetworks, SocialDecisionModifierSourceCollection.Compose(SocialInfluence, SocialEmotions));
                }

                return worldSocialDecisions;
            }
        }

        public SocialInfluenceRuntime SocialInfluence
        {
            get
            {
                if (worldSocialInfluence == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldSocialInfluence = new SocialInfluenceRuntime();
                    worldSocialInfluence.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), InterpersonalAttitudes, Reputation, SocialInteractions, new[] { playerKnowledge });
                }

                return worldSocialInfluence;
            }
        }
        public SocialEmotionRuntime SocialEmotions
        {
            get
            {
                if (worldSocialEmotions == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    worldSocialEmotions = new SocialEmotionRuntime();
                    worldSocialEmotions.Configure(GetDefinitionRegistry(), GetPrototypeSocialPersonIds(personId), Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions, SocialNorms, SocialNetworks, SocialInfluence);
                }

                return worldSocialEmotions;
            }
        }
        public FamilyRelationshipRuntime FamilyRelationships
        {
            get
            {
                if (worldFamilyRelationships == null)
                {
                    string personId = playerIdentityProgression == null ? PersistenceService.LocalPlayerId : playerIdentityProgression.PersonId;
                    string[] knownPersons = GetPrototypeSocialPersonIds(personId);
                    worldFamilyRelationships = new FamilyRelationshipRuntime();
                    worldFamilyRelationships.Configure(GetDefinitionRegistry(), knownPersons, Relationships, InterpersonalAttitudes, SocialInteractions, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeAdultPersonIds(personId));
                }

                return worldFamilyRelationships;
            }
        }
        public PersonProfessionRuntime Professions
        {
            get
            {
                if (playerProfessions == null)
                {
                    playerProfessions = new PersonProfessionRuntime();
                    playerProfessions.Configure(GetDefinitionRegistry(), new[] { playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId });
                }

                return playerProfessions;
            }
        }
        public ProfessionEntryRuntime ProfessionEntries
        {
            get
            {
                if (playerProfessionEntries == null)
                {
                    playerProfessionEntries = new ProfessionEntryRuntime();
                    playerProfessionEntries.Configure(GetDefinitionRegistry(), Professions, new[] { playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId });
                }

                return playerProfessionEntries;
            }
        }
        public TrainingRuntime Training
        {
            get
            {
                if (playerTraining == null)
                {
                    playerTraining = new TrainingRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerTraining.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, new[] { personId });
                }

                return playerTraining;
            }
        }
        public ProfessionalActivityRuntime ProfessionalActivities
        {
            get
            {
                if (playerProfessionalActivities == null)
                {
                    playerProfessionalActivities = new ProfessionalActivityRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, new[] { personId });
                }

                return playerProfessionalActivities;
            }
        }
        public CredentialRuntime Credentials
        {
            get
            {
                if (playerCredentials == null)
                {
                    playerCredentials = new CredentialRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerCredentials.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, new[] { personId }, GetPrototypeCredentialAuthorities());
                }

                return playerCredentials;
            }
        }
        public ProfessionalRankRuntime ProfessionalRanks
        {
            get
            {
                if (playerProfessionalRanks == null)
                {
                    playerProfessionalRanks = new ProfessionalRankRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerProfessionalRanks.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, new[] { personId }, GetPrototypeCredentialAuthorities());
                }

                return playerProfessionalRanks;
            }
        }
        public PositionEmploymentRuntime PositionEmployment
        {
            get
            {
                if (playerPositionEmployment == null)
                {
                    playerPositionEmployment = new PositionEmploymentRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerPositionEmployment.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, new[] { personId }, GetPrototypeOrganizations(), GetPrototypeCredentialAuthorities());
                }

                return playerPositionEmployment;
            }
        }
        public CareerHistoryRuntime CareerHistory
        {
            get
            {
                if (playerCareerHistory == null)
                {
                    playerCareerHistory = new CareerHistoryRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerCareerHistory.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, new[] { personId }, GetPrototypeOrganizations(), GetPrototypeCredentialAuthorities());
                }

                return playerCareerHistory;
            }
        }
        public LifePathRuntime LifePaths
        {
            get
            {
                if (playerLifePaths == null)
                {
                    playerLifePaths = new LifePathRuntime();
                    string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                    playerLifePaths.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, CareerHistory, new[] { personId }, GetPrototypeOrganizations());
                }

                return playerLifePaths;
            }
        }
        public ItemInstanceIdentityRuntime ItemIdentities => playerItemIdentities ??= new ItemInstanceIdentityRuntime();
        public EconomyRuntime Economy
        {
            get
            {
                if (worldEconomy == null)
                {
                    worldEconomy = new EconomyRuntime();
                }

                worldEconomy.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldEconomy;
            }
        }
        public MarketRuntime Markets
        {
            get
            {
                if (worldMarkets == null)
                {
                    worldMarkets = new MarketRuntime();
                }

                worldMarkets.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldMarkets;
            }
        }
        public TradeRuntime Trades
        {
            get
            {
                if (worldTrades == null)
                {
                    worldTrades = new TradeRuntime();
                }

                worldTrades.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldTrades;
            }
        }
        public PayrollRuntime Payroll
        {
            get
            {
                if (worldPayroll == null)
                {
                    worldPayroll = new PayrollRuntime();
                }

                worldPayroll.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldPayroll;
            }
        }
        public BusinessRuntime Businesses
        {
            get
            {
                if (worldBusinesses == null)
                {
                    worldBusinesses = new BusinessRuntime();
                }

                worldBusinesses.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldBusinesses;
            }
        }
        public PropertyRuntime Properties
        {
            get
            {
                if (worldProperties == null)
                {
                    worldProperties = new PropertyRuntime();
                }

                worldProperties.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldProperties;
            }
        }
        public ContractEconomyRuntime ContractEconomy
        {
            get
            {
                if (worldContracts == null)
                {
                    worldContracts = new ContractEconomyRuntime();
                }

                worldContracts.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldContracts;
            }
        }
        public InstitutionalRevenueRuntime InstitutionalRevenue
        {
            get
            {
                if (worldInstitutionalRevenue == null)
                {
                    worldInstitutionalRevenue = new InstitutionalRevenueRuntime();
                }

                worldInstitutionalRevenue.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldInstitutionalRevenue;
            }
        }
        public RegionalFlowRuntime RegionalFlow
        {
            get
            {
                if (worldRegionalFlow == null)
                {
                    worldRegionalFlow = new RegionalFlowRuntime();
                }

                worldRegionalFlow.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                return worldRegionalFlow;
            }
        }
        public OrganizationRuntime Organizations
        {
            get
            {
                if (worldOrganizations == null)
                {
                    worldOrganizations = new OrganizationRuntime();
                    PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(worldOrganizations, GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId);
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldOrganizations.Configure(GetDefinitionRegistry(), playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), Array.Empty<string>());
                return worldOrganizations;
            }
        }
        public OrganizationMembershipRuntime OrganizationMemberships
        {
            get
            {
                if (worldOrganizationMemberships == null)
                {
                    worldOrganizationMemberships = new OrganizationMembershipRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldOrganizationMemberships.Configure(GetDefinitionRegistry(), Organizations, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), GetPrototypeOrganizations());
                return worldOrganizationMemberships;
            }
        }
        public OrganizationAuthorityRuntime OrganizationAuthority
        {
            get
            {
                if (worldOrganizationAuthority == null)
                {
                    worldOrganizationAuthority = new OrganizationAuthorityRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldOrganizationAuthority.Configure(GetDefinitionRegistry(), Organizations, OrganizationMemberships, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), GetPrototypeOrganizations());
                return worldOrganizationAuthority;
            }
        }
        public OrganizationResourceRuntime OrganizationResources
        {
            get
            {
                if (worldOrganizationResources == null)
                {
                    worldOrganizationResources = new OrganizationResourceRuntime();
                }

                worldOrganizationResources.Configure(GetDefinitionRegistry(), Organizations, OrganizationAuthority, Economy, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, Properties, Businesses, ItemIdentities, ContractEconomy, Payroll);
                return worldOrganizationResources;
            }
        }
        public OrganizationDecisionRuntime OrganizationDecisions
        {
            get
            {
                if (worldOrganizationDecisions == null)
                {
                    worldOrganizationDecisions = new OrganizationDecisionRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldOrganizationDecisions.Configure(GetDefinitionRegistry(), Organizations, OrganizationMemberships, OrganizationAuthority, OrganizationResources, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), Economy);
                return worldOrganizationDecisions;
            }
        }
        public FactionRuntime Factions
        {
            get
            {
                if (worldFactions == null)
                {
                    worldFactions = new FactionRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldFactions.Configure(GetDefinitionRegistry(), Organizations, OrganizationMemberships, OrganizationAuthority, OrganizationResources, OrganizationDecisions, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId));
                return worldFactions;
            }
        }
        public DiplomacyRuntime Diplomacy
        {
            get
            {
                if (worldDiplomacy == null)
                {
                    worldDiplomacy = new DiplomacyRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldDiplomacy.Configure(GetDefinitionRegistry(), Organizations, Factions, OrganizationAuthority, OrganizationDecisions, OrganizationResources, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId));
                return worldDiplomacy;
            }
        }
        public GovernmentRuntime Governments
        {
            get
            {
                if (worldGovernments == null)
                {
                    worldGovernments = new GovernmentRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldGovernments.Configure(GetDefinitionRegistry(), Organizations, OrganizationMemberships, OrganizationAuthority, OrganizationDecisions, OrganizationResources, Factions, Diplomacy, Properties, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), GetKnownPlaceIds());
                return worldGovernments;
            }
        }
        public LegalRuntime Laws
        {
            get
            {
                if (worldLaws == null)
                {
                    worldLaws = new LegalRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldLaws.Configure(GetDefinitionRegistry(), Governments, Organizations, OrganizationAuthority, OrganizationDecisions, Diplomacy, Properties, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), GetKnownPlaceIds());
                return worldLaws;
            }
        }
        public CrimeRuntime Crimes
        {
            get
            {
                if (worldCrimes == null)
                {
                    worldCrimes = new CrimeRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldCrimes.Configure(GetDefinitionRegistry(), Governments, Laws, OrganizationAuthority, Diplomacy, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), GetKnownPlaceIds());
                return worldCrimes;
            }
        }
        public JusticeRuntime Justice
        {
            get
            {
                if (worldJustice == null)
                {
                    worldJustice = new JusticeRuntime();
                }

                string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
                worldJustice.Configure(GetDefinitionRegistry(), Governments, Laws, Organizations, OrganizationAuthority, Crimes, playerService == null ? PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(personId), GetKnownPlaceIds());
                return worldJustice;
            }
        }
        public ItemCompositionRuntime ItemCompositions => playerItemCompositions ??= new ItemCompositionRuntime();
        public ItemQualityAffixRuntime ItemQualityAffixes => playerItemQualityAffixes ??= new ItemQualityAffixRuntime();
        public ItemDurabilityRuntime ItemDurability => playerItemDurability ??= new ItemDurabilityRuntime();
        public ProductionRequirementRuntime ProductionRequirements => playerProductionRequirements ??= new ProductionRequirementRuntime();
        public RecipeKnowledgeRuntime RecipeKnowledge => playerRecipeKnowledge ??= new RecipeKnowledgeRuntime();
        public CraftingExecutionRuntime CraftingExecution => playerCraftingExecution ??= new CraftingExecutionRuntime();
        public ProductionWorkflowRuntime ProductionWorkflow => playerProductionWorkflow ??= new ProductionWorkflowRuntime();
        public ExperimentationRuntime Experimentation => playerExperimentation ??= new ExperimentationRuntime();
        public DefinitionRegistry ItemQualityDefinitionRegistry => GetDefinitionRegistry();
        public DefinitionRegistry ItemDurabilityDefinitionRegistry => GetDefinitionRegistry();

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (memoryMaintenance == null || playTimeTracker == null)
            {
                return;
            }

            IReadOnlyList<HistoryOperationResult> results = memoryMaintenance.AdvanceTo(playTimeTracker.CumulativeSeconds);
            if (results.Any(result => result != null && result.Succeeded && !result.Duplicate))
            {
                dirtyTracker?.MarkDirty("Player memory advanced with authoritative world time.");
            }
        }

        private void OnDisable()
        {
            if (playerService != null && inventoryEquipmentParticipant != null)
            {
                UnregisterParticipant(inventoryEquipmentParticipant);
                inventoryEquipmentParticipant = null;
            }

            if (playerService != null && itemIdentityParticipant != null)
            {
                UnregisterParticipant(itemIdentityParticipant);
                itemIdentityParticipant = null;
            }

            if (playerService != null && economyParticipant != null)
            {
                UnregisterParticipant(economyParticipant);
                economyParticipant = null;
            }

            if (playerService != null && marketParticipant != null)
            {
                UnregisterParticipant(marketParticipant);
                marketParticipant = null;
            }

            if (playerService != null && tradeParticipant != null)
            {
                UnregisterParticipant(tradeParticipant);
                tradeParticipant = null;
            }

            if (playerService != null && payrollParticipant != null)
            {
                UnregisterParticipant(payrollParticipant);
                payrollParticipant = null;
            }

            if (playerService != null && businessParticipant != null)
            {
                UnregisterParticipant(businessParticipant);
                businessParticipant = null;
            }

            if (playerService != null && propertyParticipant != null)
            {
                UnregisterParticipant(propertyParticipant);
                propertyParticipant = null;
            }

            if (playerService != null && contractEconomyParticipant != null)
            {
                UnregisterParticipant(contractEconomyParticipant);
                contractEconomyParticipant = null;
            }

            if (playerService != null && institutionalRevenueParticipant != null)
            {
                UnregisterParticipant(institutionalRevenueParticipant);
                institutionalRevenueParticipant = null;
            }

            if (playerService != null && regionalFlowParticipant != null)
            {
                UnregisterParticipant(regionalFlowParticipant);
                regionalFlowParticipant = null;
            }

            if (playerService != null && organizationParticipant != null)
            {
                UnregisterParticipant(organizationParticipant);
                organizationParticipant = null;
            }

            if (playerService != null && organizationMembershipParticipant != null)
            {
                UnregisterParticipant(organizationMembershipParticipant);
                organizationMembershipParticipant = null;
            }

            if (playerService != null && organizationAuthorityParticipant != null)
            {
                UnregisterParticipant(organizationAuthorityParticipant);
                organizationAuthorityParticipant = null;
            }

            if (playerService != null && organizationResourceParticipant != null)
            {
                UnregisterParticipant(organizationResourceParticipant);
                organizationResourceParticipant = null;
            }

            if (playerService != null && organizationDecisionParticipant != null)
            {
                UnregisterParticipant(organizationDecisionParticipant);
                organizationDecisionParticipant = null;
            }

            if (playerService != null && factionParticipant != null)
            {
                UnregisterParticipant(factionParticipant);
                factionParticipant = null;
            }

            if (playerService != null && diplomacyParticipant != null)
            {
                UnregisterParticipant(diplomacyParticipant);
                diplomacyParticipant = null;
            }

            if (playerService != null && governmentParticipant != null)
            {
                UnregisterParticipant(governmentParticipant);
                governmentParticipant = null;
            }

            if (playerService != null && legalParticipant != null)
            {
                UnregisterParticipant(legalParticipant);
                legalParticipant = null;
            }

            if (playerService != null && crimeParticipant != null)
            {
                UnregisterParticipant(crimeParticipant);
                crimeParticipant = null;
            }

            if (playerService != null && justiceParticipant != null)
            {
                UnregisterParticipant(justiceParticipant);
                justiceParticipant = null;
            }

            if (playerService != null && itemCompositionParticipant != null)
            {
                UnregisterParticipant(itemCompositionParticipant);
                itemCompositionParticipant = null;
            }

            if (playerService != null && itemQualityAffixParticipant != null)
            {
                UnregisterParticipant(itemQualityAffixParticipant);
                itemQualityAffixParticipant = null;
            }

            if (playerService != null && itemDurabilityParticipant != null)
            {
                UnregisterParticipant(itemDurabilityParticipant);
                itemDurabilityParticipant = null;
            }

            if (playerService != null && productionRequirementParticipant != null)
            {
                UnregisterParticipant(productionRequirementParticipant);
                productionRequirementParticipant = null;
            }

            if (playerService != null && recipeKnowledgeParticipant != null)
            {
                UnregisterParticipant(recipeKnowledgeParticipant);
                recipeKnowledgeParticipant = null;
            }

            if (playerService != null && craftingExecutionParticipant != null)
            {
                UnregisterParticipant(craftingExecutionParticipant);
                craftingExecutionParticipant = null;
            }

            if (playerService != null && productionWorkflowParticipant != null)
            {
                UnregisterParticipant(productionWorkflowParticipant);
                productionWorkflowParticipant = null;
            }

            if (playerService != null && experimentationParticipant != null)
            {
                UnregisterParticipant(experimentationParticipant);
                experimentationParticipant = null;
            }

            if (playerService != null && identityProgressionParticipant != null)
            {
                UnregisterParticipant(identityProgressionParticipant);
                identityProgressionParticipant = null;
            }

            if (playerService != null && playerAttributesParticipant != null)
            {
                UnregisterParticipant(playerAttributesParticipant);
                playerAttributesParticipant = null;
            }

            if (playerService != null && playerSkillsParticipant != null)
            {
                UnregisterParticipant(playerSkillsParticipant);
                playerSkillsParticipant = null;
            }

            if (playerService != null && playerTraitsParticipant != null)
            {
                UnregisterParticipant(playerTraitsParticipant);
                playerTraitsParticipant = null;
            }

            if (playerService != null && playerBodyParticipant != null)
            {
                UnregisterParticipant(playerBodyParticipant);
                playerBodyParticipant = null;
            }

            if (playerService != null && playerKnowledgeParticipant != null)
            {
                UnregisterParticipant(playerKnowledgeParticipant);
                playerKnowledgeParticipant = null;
            }

            if (worldAuthoritativeHistoryParticipant != null)
            {
                UnregisterParticipant(worldAuthoritativeHistoryParticipant);
                worldAuthoritativeHistoryParticipant = null;
            }

            if (playerMemoryParticipant != null)
            {
                UnregisterParticipant(playerMemoryParticipant);
                playerMemoryParticipant = null;
            }

            if (playerInformationSourceParticipant != null)
            {
                UnregisterParticipant(playerInformationSourceParticipant);
                playerInformationSourceParticipant = null;
            }

            if (playerInformationTransferParticipant != null)
            {
                UnregisterParticipant(playerInformationTransferParticipant);
                playerInformationTransferParticipant = null;
            }

            if (playerInformationAccessParticipant != null)
            {
                UnregisterParticipant(playerInformationAccessParticipant);
                playerInformationAccessParticipant = null;
            }

            if (playerService != null && playerProfessionParticipant != null)
            {
                UnregisterParticipant(playerProfessionParticipant);
                playerProfessionParticipant = null;
            }

            if (playerService != null && playerProfessionEntryParticipant != null)
            {
                UnregisterParticipant(playerProfessionEntryParticipant);
                playerProfessionEntryParticipant = null;
            }

            if (playerService != null && playerTrainingParticipant != null)
            {
                UnregisterParticipant(playerTrainingParticipant);
                playerTrainingParticipant = null;
            }

            if (playerService != null && playerProfessionalActivityParticipant != null)
            {
                UnregisterParticipant(playerProfessionalActivityParticipant);
                playerProfessionalActivityParticipant = null;
            }

            if (playerService != null && playerCredentialParticipant != null)
            {
                UnregisterParticipant(playerCredentialParticipant);
                playerCredentialParticipant = null;
            }

            if (playerService != null && playerProfessionalRankParticipant != null)
            {
                UnregisterParticipant(playerProfessionalRankParticipant);
                playerProfessionalRankParticipant = null;
            }

            if (playerService != null && playerPositionEmploymentParticipant != null)
            {
                UnregisterParticipant(playerPositionEmploymentParticipant);
                playerPositionEmploymentParticipant = null;
            }

            if (playerService != null && playerCareerHistoryParticipant != null)
            {
                UnregisterParticipant(playerCareerHistoryParticipant);
                playerCareerHistoryParticipant = null;
            }

            if (playerService != null && playerLifePathParticipant != null)
            {
                UnregisterParticipant(playerLifePathParticipant);
                playerLifePathParticipant = null;
            }

            if (playerService != null && playerRelationshipParticipant != null)
            {
                UnregisterParticipant(playerRelationshipParticipant);
                playerRelationshipParticipant = null;
            }

            if (playerService != null && playerInterpersonalAttitudeParticipant != null)
            {
                UnregisterParticipant(playerInterpersonalAttitudeParticipant);
                playerInterpersonalAttitudeParticipant = null;
            }

            if (playerService != null && worldReputationParticipant != null)
            {
                UnregisterParticipant(worldReputationParticipant);
                worldReputationParticipant = null;
            }

            if (playerService != null && worldRumorParticipant != null)
            {
                UnregisterParticipant(worldRumorParticipant);
                worldRumorParticipant = null;
            }

            if (playerService != null && worldSocialInteractionParticipant != null)
            {
                UnregisterParticipant(worldSocialInteractionParticipant);
                worldSocialInteractionParticipant = null;
            }

            if (playerService != null && worldSocialNormParticipant != null)
            {
                UnregisterParticipant(worldSocialNormParticipant);
                worldSocialNormParticipant = null;
            }

            if (playerService != null && worldSocialNetworkParticipant != null)
            {
                UnregisterParticipant(worldSocialNetworkParticipant);
                worldSocialNetworkParticipant = null;
            }

            if (playerService != null && worldSocialDecisionParticipant != null)
            {
                UnregisterParticipant(worldSocialDecisionParticipant);
                worldSocialDecisionParticipant = null;
            }

            if (playerService != null && worldSocialInfluenceParticipant != null)
            {
                UnregisterParticipant(worldSocialInfluenceParticipant);
                worldSocialInfluenceParticipant = null;
            }

            if (playerService != null && worldSocialEmotionParticipant != null)
            {
                UnregisterParticipant(worldSocialEmotionParticipant);
                worldSocialEmotionParticipant = null;
            }

            if (playerService != null && worldFamilyRelationshipParticipant != null)
            {
                UnregisterParticipant(worldFamilyRelationshipParticipant);
                worldFamilyRelationshipParticipant = null;
            }

            if (playerService != null && statusEffectsParticipant != null)
            {
                UnregisterParticipant(statusEffectsParticipant);
                statusEffectsParticipant = null;
            }

            if (playerService != null && playerResourcesParticipant != null)
            {
                UnregisterParticipant(playerResourcesParticipant);
                playerResourcesParticipant = null;
            }

            if (playerService != null && playerActorLifecycleParticipant != null)
            {
                UnregisterParticipant(playerActorLifecycleParticipant);
                playerActorLifecycleParticipant = null;
            }

            if (playerService != null && playerCombatExecutionParticipant != null)
            {
                UnregisterParticipant(playerCombatExecutionParticipant);
                playerCombatExecutionParticipant = null;
            }

            if (playerService != null && playerOngoingEffectsParticipant != null)
            {
                UnregisterParticipant(playerOngoingEffectsParticipant);
                playerOngoingEffectsParticipant = null;
            }

            if (playerService != null && questContractParticipant != null)
            {
                UnregisterParticipant(questContractParticipant);
                questContractParticipant = null;
            }

            if (playerService != null && playerLocationParticipant != null)
            {
                UnregisterParticipant(playerLocationParticipant);
                playerLocationParticipant = null;
            }

            if (playerService != null && playerKnowledgeRecordParticipant != null)
            {
                UnregisterParticipant(playerKnowledgeRecordParticipant);
                playerKnowledgeRecordParticipant = null;
            }

            UnsubscribeDirtyEvents();
            UnregisterWorldLocationAndNarrativePersistence();
        }

        public void ConfigurePlayerPersistence(
            DefinitionCatalog catalog,
            PlayerInventory inventory,
            PlayerEquipment equipment,
            PlayerStats stats,
            PlayerHealth health,
            PlayerMana mana,
            PlayerStamina stamina,
            StatusEffectController statusController,
            PlayerIdentityProgression identityProgression,
            PlayerQuestLog questLog,
            PlayerContractJournal contractJournal)
        {
            if (catalog != null && definitionCatalog != catalog)
            {
                definitionCatalog = catalog;
                definitionRegistry = null;
                sceneCharactersInitialized = false;
            }

            playerInventory = inventory;
            playerEquipment = equipment;
            playerStats = stats;
            playerHealth = health;
            playerMana = mana;
            playerStamina = stamina;
            statusEffectController = statusController;
            playerIdentityProgression = identityProgression;
            playerQuestLog = questLog;
            playerContractJournal = contractJournal;
            playerRoot = inventory == null ? playerRoot : inventory.transform;
        }

        public void EnsureInitialized()
        {
            EnsureRuntimeHelpers();
            playerService ??= new PersistenceService(
                PersistencePathProvider.ForPlayer(PersistenceService.LocalPlayerId),
                worldId: PersistenceService.LocalWorldId,
                playerId: PersistenceService.LocalPlayerId,
                accountId: PersistenceService.LocalAccountId,
                contextKind: PersistenceContextKind.Player);
            worldService ??= new PersistenceService(
                PersistencePathProvider.ForWorld(PersistenceService.LocalWorldId),
                worldId: PersistenceService.LocalWorldId,
                playerId: string.Empty,
                accountId: PersistenceService.LocalAccountId,
                contextKind: PersistenceContextKind.World);
            playerPersistenceContext ??= new PlayerPersistenceContext(playerService);
            worldPersistenceContext ??= new WorldPersistenceContext(worldService);
            playerService.PlaytimeSecondsProvider = () => playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds;
            playerService.MetadataProvider = new RuntimeSaveMetadataProvider(this, includePlayerSummary: true);
            worldService.MetadataProvider = new RuntimeSaveMetadataProvider(this, includePlayerSummary: false);

            EnsurePlayerIdentityProgressionParticipant();
            EnsurePlayerAttributesParticipant();
            EnsurePlayerSkillsParticipant();
            EnsurePlayerTraitsParticipant();
            EnsurePlayerBodyParticipant();
            EnsurePlayerKnowledgeParticipant();
            EnsureWorldAuthoritativeHistoryParticipant();
            EnsurePlayerMemoryParticipant();
            EnsureKnowledgeHistoryEventBridge();
            EnsurePlayerProfessionParticipant();
            EnsurePlayerProfessionEntryParticipant();
            EnsurePlayerInformationSourceParticipant();
            EnsurePlayerInformationTransferParticipant();
            EnsurePlayerTrainingParticipant();
            EnsurePlayerProfessionalActivityParticipant();
            EnsurePlayerCredentialParticipant();
            EnsurePlayerProfessionalRankParticipant();
            EnsurePlayerPositionEmploymentParticipant();
            EnsurePlayerCareerHistoryParticipant();
            EnsurePlayerLifePathParticipant();
            EnsurePlayerRelationshipParticipant();
            EnsurePlayerInterpersonalAttitudeParticipant();
            EnsureWorldReputationParticipant();
            EnsureWorldRumorParticipant();
            EnsureWorldSocialInteractionParticipant();
            EnsureWorldSocialNormParticipant();
            EnsureWorldSocialNetworkParticipant();
            EnsureWorldSocialInfluenceParticipant();
            EnsureWorldSocialEmotionParticipant();
            EnsureWorldFamilyRelationshipParticipant();
            EnsureWorldSocialDecisionParticipant();
            EnsurePlayerInformationAccessParticipant();
            EnsurePlayerKnowledgeRecordParticipant();
            EnsurePlayerItemIdentityParticipant();
            EnsureWorldEconomyParticipant();
            EnsureWorldMarketParticipant();
            EnsureWorldTradeParticipant();
            EnsureWorldPayrollParticipant();
            EnsureWorldBusinessParticipant();
            EnsureWorldPropertyParticipant();
            EnsureWorldContractEconomyParticipant();
            EnsureWorldInstitutionalRevenueParticipant();
            EnsureWorldRegionalFlowParticipant();
            EnsureWorldOrganizationParticipant();
            EnsureWorldOrganizationMembershipParticipant();
            EnsureWorldOrganizationAuthorityParticipant();
            EnsureWorldOrganizationResourceParticipant();
            EnsureWorldOrganizationDecisionParticipant();
            EnsureWorldFactionParticipant();
            EnsureWorldDiplomacyParticipant();
            EnsureWorldGovernmentParticipant();
            EnsureWorldLegalParticipant();
            EnsureWorldCrimeParticipant();
            EnsureWorldJusticeParticipant();
            EnsureWorldLocationAndNarrativePersistence();
            EnsurePlayerItemCompositionParticipant();
            EnsurePlayerItemQualityAffixParticipant();
            EnsurePlayerItemDurabilityParticipant();
            EnsurePlayerProductionRequirementParticipant();
            EnsurePlayerRecipeKnowledgeParticipant();
            EnsurePlayerCraftingExecutionParticipant();
            EnsurePlayerProductionWorkflowParticipant();
            EnsurePlayerExperimentationParticipant();
            EnsurePlayerInventoryEquipmentParticipant();
            EnsurePlayerStatusEffectsParticipant();
            EnsurePlayerResourcesParticipant();
            EnsurePlayerActorLifecycleParticipant();
            EnsurePlayerCombatExecutionParticipant();
            EnsurePlayerOngoingEffectsParticipant();
            EnsurePlayerQuestContractParticipant();
            EnsurePlayerLocationParticipant();
            EnsurePersistenceConsistencyValidators();
            InitializeSceneCharacters();
            PlayerReadiness = playerPersistenceContext.BuildReadiness(new[]
            {
                PlayerIdentityProgressionPersistenceParticipant.Key,
                PlayerAttributesPersistenceParticipant.Key,
                PlayerSkillsPersistenceParticipant.Key,
                PersonKnowledgePersistenceParticipant.Key,
                PersonMemoryPersistenceParticipant.Key,
                PlayerInventoryEquipmentPersistenceParticipant.Key,
                PlayerStatusEffectsPersistenceParticipant.Key,
                PlayerResourcesPersistenceParticipant.Key,
                PlayerQuestContractPersistenceParticipant.Key
            });
            WorldReadiness = worldPersistenceContext.BuildReadiness(new[]
            {
                AuthoritativeHistoryPersistenceParticipant.Key,
                InformationSourcePersistenceParticipant.WorldKey,
                InformationTransferPersistenceParticipant.WorldKey,
                InformationAccessPersistenceParticipant.WorldKey,
                KnowledgeRecordPersistenceParticipant.WorldKey,
                LocationPersistenceParticipant.Key,
                EntityLocationPersistenceParticipant.Key,
                InteractionPointPersistenceParticipant.Key,
                LocationConnectionPersistenceParticipant.Key,
                LocationRoutePersistenceParticipant.Key,
                TravelJourneyPersistenceParticipant.Key,
                TravelConditionPersistenceParticipant.Key,
                PoliticalTravelPersistenceParticipant.Key,
                QuestRuntimePersistenceParticipant.Key,
                QuestParticipationRuntimePersistenceParticipant.Key,
                QuestObjectiveProgressPersistenceParticipant.Key,
                QuestOutcomePersistenceParticipant.Key,
                QuestSourcePersistenceParticipant.Key,
                ConversationPersistenceParticipant.Key,
                DialogueFlowPersistenceParticipant.Key,
                NarrativeEventPersistenceParticipant.Key,
                NarrativeStatePersistenceParticipant.Key,
                NarrativeArcPersistenceParticipant.Key
            });
            SubscribeDirtyEvents();
        }

        private void EnsureKnowledgeHistoryEventBridge()
        {
            if (worldAuthoritativeHistoryParticipant == null || playerMemoryParticipant == null)
            {
                return;
            }

            knowledgeHistoryEventBridge ??= new KnowledgeHistoryEventBridge(
                AuthoritativeHistory,
                () => playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds,
                ResolvePlayerPersonId,
                () => playerBody == null ? string.Empty : playerBody.ActorBodyId,
                () => currentPlaceTracker == null ? string.Empty : currentPlaceTracker.CurrentPlaceId);

            if (!characterCreationHistoryEnsured && playerIdentityProgression != null)
            {
                HistoryOperationResult result = knowledgeHistoryEventBridge.EnsureCharacterCreation(
                    playerIdentityProgression.Origin?.originId,
                    playerIdentityProgression.BirthGift?.giftDefinitionId);
                characterCreationHistoryEnsured = result != null && (result.Succeeded || result.Duplicate);
            }
        }

        private void InitializeSceneCharacters()
        {
            if (sceneCharactersInitialized)
            {
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry == null)
            {
                return;
            }

            foreach (CharacterSystemCoordinator character in FindObjectsByType<CharacterSystemCoordinator>(FindObjectsInactive.Include))
            {
                if (character != null && !character.IsReady)
                {
                    character.InitializeFromRegistry(registry, restoring: false);
                }
            }

            sceneCharactersInitialized = true;
        }

        public IReadOnlyList<SaveSlotMetadata> ListSaveSlots()
        {
            EnsureInitialized();
            return playerService.ListSaveSlots();
        }

        public IReadOnlyList<SaveSlotDescriptor> BuildSaveSlotDescriptors()
        {
            EnsureInitialized();
            return PrototypeSaveSlotCatalog.BuildDescriptors(playerService, ManualSlotCount, AutosaveSlotCount);
        }

        public SaveEligibilityResult CheckSaveEligibility(bool showDetailedPlayerMessage, bool allowOpenMenu = true)
        {
            EnsureInitialized();
            if (playerService.OperationInProgress)
            {
                return SaveEligibilityResult.Block(SaveEligibilityStatus.OperationInProgress, "A persistence operation is already running.");
            }

            if (PlayerReadiness == null || !PlayerReadiness.succeeded)
            {
                return SaveEligibilityResult.Block(SaveEligibilityStatus.ParticipantCaptureFailed, PlayerReadiness?.message ?? "Player persistence has not initialized successfully.");
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return SaveEligibilityResult.Block(SaveEligibilityStatus.SceneTransition, "Saving is unavailable while the active scene is changing.");
            }

            if (!allowOpenMenu && inventoryScreenController is IPlayerMenuController menuController && menuController.IsOpen)
            {
                return SaveEligibilityResult.Block(SaveEligibilityStatus.ModalBlocked, "Autosave is deferred while a modal player menu is open.");
            }

            ResolvePlayerPersistenceReferences();
            if (playerRoot == null)
            {
                return SaveEligibilityResult.Block(SaveEligibilityStatus.NoActivePlayer, "No active player root is available.");
            }

            if (playerHealth != null && playerHealth.IsDefeated)
            {
                return SaveEligibilityResult.Block(SaveEligibilityStatus.InvalidPlayerState, "Cannot save while the player is defeated.");
            }

            return SaveEligibilityResult.Allow(showDetailedPlayerMessage ? "Saving is available." : "Allowed");
        }

        public PersistenceSaveResult SaveManualSlot(int zeroBasedIndex)
        {
            string slotId = PrototypeSaveSlotCatalog.ManualSlotId(zeroBasedIndex);
            PersistenceSaveResult playerResult = SaveNamedSlot(slotId, PrototypeSaveSlotCatalog.ManualDisplayName(zeroBasedIndex), markClean: true);
            return playerResult.Succeeded
                ? CombineWithWorldCheckpoint(playerResult, $"Manual slot {zeroBasedIndex + 1}")
                : playerResult;
        }

        public PersistenceSaveResult SaveNamedSlot(string slotId, string displayName, bool markClean)
        {
            EnsureInitialized();
            SaveEligibilityResult eligibility = CheckSaveEligibility(showDetailedPlayerMessage: true);
            if (!eligibility.Allowed)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.ParticipantCaptureFailed, slotId, string.Empty, eligibility.Message);
            }

            PersistenceSaveResult result = playerService.Save(slotId, displayName);
            Report(result.Succeeded, result.Message);
            if (result.Succeeded && markClean)
            {
                dirtyTracker?.MarkClean($"Saved {displayName}.");
                autosaveCoordinator?.ResetTimer();
            }

            return result;
        }

        public PersistenceSaveResult SaveWorldCheckpoint(string reason = "Scheduled")
        {
            EnsureInitialized();
            if (WorldReadiness == null || !WorldReadiness.succeeded)
            {
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.DependencyValidationFailed, PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId, string.Empty, WorldReadiness?.message ?? "World persistence has not initialized successfully.");
            }

            PersistenceSaveResult result = worldService.Save(
                PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId,
                $"World Checkpoint ({reason})");
            Report(result.Succeeded, result.Message);
            return result;
        }

        public PersistenceLoadResult LoadWorldCheckpoint(bool loadBackup = false)
        {
            EnsureInitialized();
            PersistenceLoadResult result = worldService.Load(PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId, loadBackup);
            Report(result.Succeeded, result.Message);
            return result;
        }

        public PersistenceSaveResult SaveAutosave(string reason)
        {
            EnsureInitialized();
            string staging = PrototypeSaveSlotCatalog.AutosaveStagingSlotId;
            PersistenceSaveResult saveResult = SaveNamedSlot(staging, $"Autosave ({reason})", markClean: false);
            if (!saveResult.Succeeded)
            {
                return saveResult;
            }

            PersistenceSaveResult rotate = playerService.RotateAutosaveSlots(staging, PrototypeSaveSlotCatalog.BuildAutosaveSlotIds(AutosaveSlotCount));
            Report(rotate.Succeeded, rotate.Message);
            if (rotate.Succeeded)
            {
                dirtyTracker?.MarkClean($"Autosaved: {reason}.");
                return CombineWithWorldCheckpoint(rotate, $"Autosave: {reason}");
            }

            return rotate;
        }

        private PersistenceSaveResult CombineWithWorldCheckpoint(PersistenceSaveResult playerResult, string reason)
        {
            PersistenceSaveResult worldResult = SaveWorldCheckpoint(reason);
            if (!worldResult.Succeeded)
            {
                return PersistenceSaveResult.Failure(
                    worldResult.Status,
                    playerResult.SlotId,
                    playerResult.Path,
                    $"The player save completed, but the durable world checkpoint failed: {worldResult.Message}",
                    worldResult.Exception,
                    worldResult.TransactionId,
                    worldResult.Phase);
            }

            return PersistenceSaveResult.Success(
                playerResult.SlotId,
                playerResult.Path,
                $"{playerResult.Message} World knowledge and history were checkpointed successfully.",
                playerResult.TransactionId,
                playerResult.Phase);
        }

        public PersistenceSaveResult ForceAutosave(string reason = "DevelopmentCommand")
        {
            EnsureInitialized();
            return autosaveCoordinator == null ? SaveAutosave(reason) : autosaveCoordinator.ForceAutosave(reason);
        }

        public PersistenceLoadResult LoadSaveSlot(string slotId, bool loadBackup = false)
        {
            EnsureInitialized();
            PersistenceValidationResult preValidation = playerService.ValidateSlot(slotId, loadBackup);
            PersistenceLoadResult result = playerService.Load(slotId, loadBackup);
            Report(result.Succeeded, result.Message);
            if (result.Succeeded)
            {
                playTimeTracker?.Restore(preValidation.Envelope == null ? 0d : preValidation.Envelope.playtimeSeconds);
                dirtyTracker?.MarkClean(loadBackup ? "Loaded backup save." : "Loaded save.");
                autosaveCoordinator?.ResetTimer();
            }

            return result;
        }

        public PersistenceValidationResult ValidateSaveSlot(string slotId, bool validateBackup = false)
        {
            EnsureInitialized();
            PersistenceValidationResult result = playerService.ValidateSlot(slotId, validateBackup);
            Report(result.Succeeded, result.Message);
            return result;
        }

        public PersistenceDeleteResult DeleteSaveSlot(string slotId)
        {
            EnsureInitialized();
            PersistenceDeleteResult result = playerService.DeleteSlot(slotId);
            Report(result.Succeeded, result.Message);
            return result;
        }

        public void SetAutosaveIntervalForTesting(float seconds)
        {
            autosaveIntervalSeconds = Mathf.Max(5f, seconds);
            autosaveCoordinator?.SetIntervalForTesting(autosaveIntervalSeconds);
        }

        public string BuildSaveSlotDiagnosticSummary()
        {
            EnsureInitialized();
            PersistenceTransactionDiagnostics diagnostics = playerService.BuildTransactionDiagnostics();
            return $"Operation={playerService.OperationState} Phase={diagnostics.phase} Safety={diagnostics.runtimeSafety} Dirty={dirtyTracker != null && dirtyTracker.IsDirty} PlayTime={PrototypeSaveSlotCatalog.FormatPlayTime(playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds)} Autosave={autosaveCoordinator?.LastResult ?? "None"}";
        }

        public string BuildPersistenceIntegrationDiagnosticSummary()
        {
            EnsureInitialized();
            PersistenceDependencyReport dependencies = playerService.BuildParticipantDependencyReport();
            PersistenceTransactionDiagnostics diagnostics = playerService.BuildTransactionDiagnostics();
            string order = dependencies.orderedParticipantKeys == null || dependencies.orderedParticipantKeys.Length == 0
                ? "None"
                : string.Join(" -> ", dependencies.orderedParticipantKeys);
            return string.Join("\n", new[]
            {
                "Persistence Integration",
                $"Transaction: {diagnostics.transactionId}",
                $"Phase: {diagnostics.phase}",
                $"Operation: {diagnostics.operationState}",
                $"Safety: {diagnostics.runtimeSafety}",
                $"Guard Active: {PersistenceRestorationGuard.IsActive}",
                $"Participant Dependencies: {(dependencies.succeeded ? "Valid" : "Invalid")}",
                $"Participant Order: {order}",
                $"Dependency Detail: {dependencies.message}",
                $"Fingerprint: {BuildRuntimeStateFingerprint()}",
                $"Last Audit: {diagnostics.lastConsistencyAudit}",
                $"Last Recovery: {diagnostics.lastRecoveryRecommendation}"
            });
        }

        public string BuildRuntimeStateFingerprint()
        {
            EnsureInitialized();
            return playerService.BuildRuntimeStateFingerprint();
        }

        public SaveRecoveryScanReport RunRecoveryScan()
        {
            EnsureInitialized();
            SaveRecoveryScanReport report = playerService.ScanRecoverySources();
            Report(true, report.recommendation);
            return report;
        }

        public PersistenceSaveResult PromoteBackup(string slotId)
        {
            EnsureInitialized();
            PersistenceSaveResult result = playerService.PromoteBackup(slotId);
            Report(result.Succeeded, result.Message);
            return result;
        }

        public PersistenceSaveResult QuarantinePrimary(string slotId)
        {
            EnsureInitialized();
            PersistenceSaveResult result = playerService.QuarantinePrimary(slotId);
            Report(result.Succeeded, result.Message);
            return result;
        }

        public PersistenceDeleteResult CleanupStaleTemporaryFiles()
        {
            EnsureInitialized();
            PersistenceDeleteResult result = playerService.CleanupStaleTemporaryFiles();
            Report(result.Succeeded, result.Message);
            return result;
        }

        public void InjectNextPersistenceFault(PersistenceFaultInjectionPoint point)
        {
            EnsureInitialized();
            playerService.FaultInjection.nextFailurePoint = point;
            playerService.FaultInjection.message = $"Injected {point} fault.";
            Report(true, $"Next persistence fault: {point}");
        }

        private static void Report(bool succeeded, string message, bool failureAsInfo = false)
        {
            if (succeeded || failureAsInfo)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }

            PrototypeHudMessageBus.Show(message);
        }

        private bool RegisterParticipant(IPersistenceParticipant participant, out string failureReason)
        {
            failureReason = string.Empty;
            if (participant == null)
            {
                failureReason = "Cannot register a null persistence participant.";
                return false;
            }

            PersistenceRuntimeContext context = participant.Scope == PersistenceScope.Player
                ? playerPersistenceContext
                : participant.Scope == PersistenceScope.SharedWorld || participant.Scope == PersistenceScope.RegionOrScene
                    ? worldPersistenceContext
                    : null;
            if (context == null)
            {
                failureReason = $"No runtime persistence context owns scope {participant.Scope} for '{participant.ParticipantKey}'.";
                return false;
            }

            return context.Register(participant, out failureReason);
        }

        private void UnregisterParticipant(IPersistenceParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            if (participant.Scope == PersistenceScope.Player)
            {
                playerPersistenceContext?.Unregister(participant);
            }
            else
            {
                worldPersistenceContext?.Unregister(participant);
            }
        }

        private sealed class RuntimeSaveMetadataProvider : ISaveMetadataProvider
        {
            private readonly PrototypePersistenceServiceBehaviour owner;
            private readonly bool includePlayerSummary;

            public RuntimeSaveMetadataProvider(PrototypePersistenceServiceBehaviour owner, bool includePlayerSummary)
            {
                this.owner = owner;
                this.includePlayerSummary = includePlayerSummary;
            }

            public SaveMetadataSnapshot CaptureMetadata()
            {
                Transform root = owner.playerRoot;
                Vector3 position = root == null ? Vector3.zero : root.position;
                return new SaveMetadataSnapshot
                {
                    SceneId = owner.ResolveSceneKey(),
                    PlaceId = owner.currentPlaceTracker == null ? string.Empty : owner.currentPlaceTracker.CurrentPlaceId,
                    PlayerSummary = !includePlayerSummary || root == null
                        ? string.Empty
                        : $"Position {position.x:0.##}, {position.y:0.##}, {position.z:0.##}"
                };
            }
        }

        private void EnsurePlayerInventoryEquipmentParticipant()
        {
            if (!registerPlayerInventoryEquipment || inventoryEquipmentParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerInventory == null || playerEquipment == null)
            {
                Debug.LogWarning("Player inventory/equipment persistence participant was not registered because the prototype player inventory or equipment component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player inventory/equipment persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            inventoryEquipmentParticipant = new PlayerInventoryEquipmentPersistenceParticipant(
                playerInventory,
                playerEquipment,
                GetDefinitionRegistry,
                playerService.PlayerId,
                registerPlayerItemIdentities ? ItemIdentities : null,
                "prototype.player.inventory-equipment");

            RegisterParticipant(inventoryEquipmentParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                inventoryEquipmentParticipant = null;
            }
        }

        private void EnsurePlayerItemIdentityParticipant()
        {
            if (!registerPlayerItemIdentities || itemIdentityParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player item identity persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            EnsurePlayerItemIdentitySynchronizer();
            itemIdentityParticipant = new ItemInstanceIdentityPersistenceParticipant(
                ItemIdentities,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(itemIdentityParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                itemIdentityParticipant = null;
            }
        }

        private void EnsureWorldEconomyParticipant()
        {
            if (!registerWorldEconomy || economyParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World economy persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            economyParticipant = new EconomyPersistenceParticipant(
                Economy,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(economyParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                economyParticipant = null;
            }
        }

        private void EnsureWorldMarketParticipant()
        {
            if (!registerWorldMarkets || marketParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World market persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            marketParticipant = new MarketPersistenceParticipant(
                Markets,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(marketParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                marketParticipant = null;
            }
        }

        private void EnsureWorldTradeParticipant()
        {
            if (!registerWorldTrades || tradeParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World trade persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            tradeParticipant = new TradePersistenceParticipant(
                Trades,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(tradeParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                tradeParticipant = null;
            }
        }

        private void EnsureWorldPayrollParticipant()
        {
            if (!registerWorldPayroll || payrollParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World payroll persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            payrollParticipant = new PayrollPersistenceParticipant(
                Payroll,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(payrollParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                payrollParticipant = null;
            }
        }

        private void EnsureWorldBusinessParticipant()
        {
            if (!registerWorldBusinesses || businessParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World business persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            businessParticipant = new BusinessPersistenceParticipant(
                Businesses,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(businessParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                businessParticipant = null;
            }
        }

        private void EnsureWorldPropertyParticipant()
        {
            if (!registerWorldProperties || propertyParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World property persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            propertyParticipant = new PropertyPersistenceParticipant(
                Properties,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(propertyParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                propertyParticipant = null;
            }
        }

        private void EnsureWorldContractEconomyParticipant()
        {
            if (!registerWorldContracts || contractEconomyParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World contract economy persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            contractEconomyParticipant = new ContractEconomyPersistenceParticipant(
                ContractEconomy,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(contractEconomyParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                contractEconomyParticipant = null;
            }
        }

        private void EnsureWorldInstitutionalRevenueParticipant()
        {
            if (!registerWorldInstitutionalRevenue || institutionalRevenueParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World institutional revenue persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            institutionalRevenueParticipant = new InstitutionalRevenuePersistenceParticipant(
                InstitutionalRevenue,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(institutionalRevenueParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                institutionalRevenueParticipant = null;
            }
        }

        private void EnsureWorldRegionalFlowParticipant()
        {
            if (!registerWorldRegionalFlow || regionalFlowParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World regional flow persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            regionalFlowParticipant = new RegionalFlowPersistenceParticipant(
                RegionalFlow,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(regionalFlowParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                regionalFlowParticipant = null;
            }
        }

        private void EnsureWorldOrganizationParticipant()
        {
            if (!registerWorldOrganizations || organizationParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World organization persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            organizationParticipant = new OrganizationPersistenceParticipant(
                Organizations,
                GetDefinitionRegistry,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(playerService.PlayerId),
                () => Array.Empty<string>());

            RegisterParticipant(organizationParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                organizationParticipant = null;
            }
        }

        private void EnsureWorldOrganizationMembershipParticipant()
        {
            if (!registerWorldOrganizationMemberships || organizationMembershipParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World organization membership persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            organizationMembershipParticipant = new OrganizationMembershipPersistenceParticipant(
                OrganizationMemberships,
                GetDefinitionRegistry,
                () => Organizations,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(playerService.PlayerId),
                GetPrototypeOrganizations);

            RegisterParticipant(organizationMembershipParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                organizationMembershipParticipant = null;
            }
        }

        private void EnsureWorldOrganizationAuthorityParticipant()
        {
            if (!registerWorldOrganizationAuthority || organizationAuthorityParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World organization authority persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            organizationAuthorityParticipant = new OrganizationAuthorityPersistenceParticipant(
                OrganizationAuthority,
                GetDefinitionRegistry,
                () => Organizations,
                () => OrganizationMemberships,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(playerService.PlayerId),
                GetPrototypeOrganizations);

            RegisterParticipant(organizationAuthorityParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                organizationAuthorityParticipant = null;
            }
        }

        private void EnsureWorldOrganizationResourceParticipant()
        {
            if (!registerWorldOrganizationResources || organizationResourceParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World organization resource persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            organizationResourceParticipant = new OrganizationResourcePersistenceParticipant(
                OrganizationResources,
                GetDefinitionRegistry,
                () => Organizations,
                () => OrganizationAuthority,
                () => Economy,
                playerService.WorldId,
                () => Properties,
                () => Businesses,
                () => ItemIdentities,
                () => ContractEconomy,
                () => Payroll);

            RegisterParticipant(organizationResourceParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                organizationResourceParticipant = null;
            }
        }

        private void EnsureWorldOrganizationDecisionParticipant()
        {
            if (!registerWorldOrganizationDecisions || organizationDecisionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World organization decision persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            organizationDecisionParticipant = new OrganizationDecisionPersistenceParticipant(
                OrganizationDecisions,
                GetDefinitionRegistry,
                () => Organizations,
                () => OrganizationMemberships,
                () => OrganizationAuthority,
                () => OrganizationResources,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId));

            RegisterParticipant(organizationDecisionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                organizationDecisionParticipant = null;
            }
        }

        private void EnsureWorldFactionParticipant()
        {
            if (!registerWorldFactions || factionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World faction persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            factionParticipant = new FactionPersistenceParticipant(
                Factions,
                GetDefinitionRegistry,
                () => Organizations,
                () => OrganizationMemberships,
                () => OrganizationAuthority,
                () => OrganizationResources,
                () => OrganizationDecisions,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId));

            RegisterParticipant(factionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                factionParticipant = null;
            }
        }

        private void EnsureWorldDiplomacyParticipant()
        {
            if (!registerWorldDiplomacy || diplomacyParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World diplomacy persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            diplomacyParticipant = new DiplomacyPersistenceParticipant(
                Diplomacy,
                GetDefinitionRegistry,
                () => Organizations,
                () => Factions,
                () => OrganizationAuthority,
                () => OrganizationDecisions,
                () => OrganizationResources,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId));

            RegisterParticipant(diplomacyParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                diplomacyParticipant = null;
            }
        }

        private void EnsureWorldGovernmentParticipant()
        {
            if (!registerWorldGovernments || governmentParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World government persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            governmentParticipant = new GovernmentPersistenceParticipant(
                Governments,
                GetDefinitionRegistry,
                () => Organizations,
                () => OrganizationMemberships,
                () => OrganizationAuthority,
                () => OrganizationDecisions,
                () => OrganizationResources,
                () => Factions,
                () => Diplomacy,
                () => Properties,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId),
                GetKnownPlaceIds);

            RegisterParticipant(governmentParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                governmentParticipant = null;
            }
        }

        private void EnsureWorldLegalParticipant()
        {
            if (!registerWorldLaws || legalParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World legal persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            legalParticipant = new LegalPersistenceParticipant(
                Laws,
                GetDefinitionRegistry,
                () => Governments,
                () => Organizations,
                () => OrganizationAuthority,
                () => OrganizationDecisions,
                () => Diplomacy,
                () => Properties,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId),
                GetKnownPlaceIds);

            RegisterParticipant(legalParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                legalParticipant = null;
            }
        }

        private void EnsureWorldCrimeParticipant()
        {
            if (!registerWorldCrimes || crimeParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World crime persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            crimeParticipant = new CrimePersistenceParticipant(
                Crimes,
                GetDefinitionRegistry,
                () => Governments,
                () => Laws,
                () => OrganizationAuthority,
                () => Diplomacy,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId),
                GetKnownPlaceIds);

            RegisterParticipant(crimeParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                crimeParticipant = null;
            }
        }

        private void EnsureWorldJusticeParticipant()
        {
            if (!registerWorldJustice || justiceParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("World justice persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId;
            justiceParticipant = new JusticePersistenceParticipant(
                Justice,
                GetDefinitionRegistry,
                () => Governments,
                () => Laws,
                () => Organizations,
                () => OrganizationAuthority,
                () => Crimes,
                playerService.WorldId,
                () => GetPrototypeSocialPersonIds(personId),
                GetKnownPlaceIds);

            RegisterParticipant(justiceParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                justiceParticipant = null;
            }
        }

        private void EnsurePlayerItemCompositionParticipant()
        {
            if (!registerPlayerItemCompositions || itemCompositionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (!registerPlayerItemIdentities)
            {
                Debug.LogWarning("Player item composition persistence participant was not registered because item identity persistence is disabled.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player item composition persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            itemCompositionParticipant = new ItemCompositionPersistenceParticipant(
                ItemCompositions,
                ItemIdentities,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(itemCompositionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                itemCompositionParticipant = null;
            }
        }

        private void EnsurePlayerItemQualityAffixParticipant()
        {
            if (!registerPlayerItemQualityAffixes || itemQualityAffixParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (!registerPlayerItemIdentities)
            {
                Debug.LogWarning("Player item quality persistence participant was not registered because item identity persistence is disabled.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player item quality persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            itemQualityAffixParticipant = new ItemQualityAffixPersistenceParticipant(
                ItemQualityAffixes,
                ItemIdentities,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(itemQualityAffixParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                itemQualityAffixParticipant = null;
            }
        }

        private void EnsurePlayerItemDurabilityParticipant()
        {
            if (!registerPlayerItemDurability || itemDurabilityParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (!registerPlayerItemIdentities)
            {
                Debug.LogWarning("Player item durability persistence participant was not registered because item identity persistence is disabled.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player item durability persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            itemDurabilityParticipant = new ItemDurabilityPersistenceParticipant(
                ItemDurability,
                ItemIdentities,
                registerPlayerItemCompositions ? ItemCompositions : null,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(itemDurabilityParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                itemDurabilityParticipant = null;
            }
        }

        private void EnsurePlayerRecipeKnowledgeParticipant()
        {
            if (!registerPlayerRecipeKnowledge || recipeKnowledgeParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player recipe knowledge persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            recipeKnowledgeParticipant = new RecipeKnowledgePersistenceParticipant(
                RecipeKnowledge,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(recipeKnowledgeParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                recipeKnowledgeParticipant = null;
            }
        }

        private void EnsurePlayerProductionRequirementParticipant()
        {
            if (!registerPlayerProductionRequirements || productionRequirementParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player production requirement persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            productionRequirementParticipant = new ProductionRequirementPersistenceParticipant(
                ProductionRequirements,
                playerService.WorldId);

            RegisterParticipant(productionRequirementParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                productionRequirementParticipant = null;
            }
        }

        private void EnsurePlayerCraftingExecutionParticipant()
        {
            if (!registerPlayerCraftingExecution || craftingExecutionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (!registerPlayerItemIdentities || !registerPlayerRecipeKnowledge)
            {
                Debug.LogWarning("Player crafting execution persistence participant was not registered because item identity or recipe persistence is disabled.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player crafting execution persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            craftingExecutionParticipant = new CraftingExecutionPersistenceParticipant(
                CraftingExecution,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(craftingExecutionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                craftingExecutionParticipant = null;
            }
        }

        private void EnsurePlayerProductionWorkflowParticipant()
        {
            if (!registerPlayerProductionWorkflow || productionWorkflowParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (!registerPlayerItemIdentities || !registerPlayerProductionRequirements || !registerPlayerCraftingExecution)
            {
                Debug.LogWarning("Player production workflow persistence participant was not registered because item identity, production requirement, or crafting execution persistence is disabled.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player production workflow persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            productionWorkflowParticipant = new ProductionWorkflowPersistenceParticipant(
                ProductionWorkflow,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(productionWorkflowParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                productionWorkflowParticipant = null;
            }
        }

        private void EnsurePlayerExperimentationParticipant()
        {
            if (!registerPlayerExperimentation || experimentationParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (!registerPlayerItemIdentities || !registerPlayerProductionRequirements || !registerPlayerCraftingExecution)
            {
                Debug.LogWarning("Player experimentation persistence participant was not registered because item identity, production requirement, or crafting execution persistence is disabled.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player experimentation persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            experimentationParticipant = new ExperimentationPersistenceParticipant(
                Experimentation,
                GetDefinitionRegistry,
                playerService.WorldId);

            RegisterParticipant(experimentationParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                experimentationParticipant = null;
            }
        }

        private void EnsurePlayerItemIdentitySynchronizer()
        {
            ResolvePlayerPersistenceReferences();
            if (playerInventory == null || playerEquipment == null)
            {
                return;
            }

            playerItemIdentitySynchronizer = playerInventory.GetComponent<PlayerItemIdentitySynchronizer>();
            if (playerItemIdentitySynchronizer == null)
            {
                Debug.LogWarning("Player item identity synchronization is unavailable because PlayerItemIdentitySynchronizer is not authored on the player.");
                return;
            }

            playerItemIdentitySynchronizer.Configure(
                playerInventory,
                playerEquipment,
                ItemIdentities,
                GetDefinitionRegistry,
                playerService.PlayerId,
                "prototype.player.inventory-equipment");
            ItemIdentityInventoryBridgeResult synchronization = playerItemIdentitySynchronizer.SynchronizeNow();
            if (!synchronization.Succeeded)
            {
                Debug.LogWarning($"Player item identity synchronization failed: {synchronization.Message}");
            }
        }

        private void EnsurePlayerIdentityProgressionParticipant()
        {
            if (!registerPlayerIdentityProgression || identityProgressionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null)
            {
                Debug.LogWarning("Player identity/progression persistence participant was not registered because the prototype player identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player identity/progression persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            playerIdentityProgression.ConfigureIdentity(playerService.AccountId, playerService.PlayerId);
            playerIdentityProgression.ConfigureDefinitions(registry);

            identityProgressionParticipant = new PlayerIdentityProgressionPersistenceParticipant(
                playerIdentityProgression,
                GetDefinitionRegistry,
                playerService.PlayerId,
                playerService.AccountId);

            RegisterParticipant(identityProgressionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                identityProgressionParticipant = null;
            }
        }

        private void EnsureRuntimeHelpers()
        {
            if (playTimeTracker == null)
            {
                playTimeTracker = GetComponent<PlayTimeTracker>();
            }

            if (dirtyTracker == null)
            {
                dirtyTracker = GetComponent<GameSaveDirtyTracker>();
            }

            if (autosaveCoordinator == null)
            {
                autosaveCoordinator = GetComponent<AutosaveCoordinator>();
            }

            autosaveCoordinator?.Configure(this, autosaveIntervalSeconds);
        }

        private void EnsurePlayerAttributesParticipant()
        {
            if (!registerPlayerAttributes || playerAttributesParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerAttributes == null || playerIdentityProgression == null)
            {
                Debug.LogWarning("Player attributes persistence participant was not registered because the prototype player attributes or identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player attributes persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            playerAttributesParticipant = new PlayerAttributesPersistenceParticipant(
                playerAttributes,
                playerIdentityProgression,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(playerAttributesParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerAttributesParticipant = null;
            }
        }

        private void EnsurePlayerSkillsParticipant()
        {
            if (!registerPlayerSkills || playerSkillsParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerSkills == null || playerIdentityProgression == null)
            {
                Debug.LogWarning("Player Skills persistence participant was not registered because the prototype player Skill collection or identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player Skills persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            playerSkillsParticipant = new PlayerSkillsPersistenceParticipant(
                playerSkills,
                playerIdentityProgression,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(playerSkillsParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerSkillsParticipant = null;
            }
        }

        private void EnsurePlayerTraitsParticipant()
        {
            if (!registerPlayerTraits || playerTraitsParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerTraits == null || playerCalculatedStats == null)
            {
                Debug.LogWarning("Player Traits persistence participant was not registered because the prototype player Trait collection or calculated stats are missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player Traits persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            playerTraitsParticipant = new PlayerTraitsPersistenceParticipant(
                playerTraits,
                playerIdentityProgression,
                playerCalculatedStats,
                playerSkills,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(playerTraitsParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerTraitsParticipant = null;
            }
        }

        private void EnsurePlayerBodyParticipant()
        {
            if (!registerPlayerBody || playerBodyParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerBody == null || playerTraits == null || playerCalculatedStats == null)
            {
                Debug.LogWarning("Player body persistence participant was not registered because the body runtime, Trait collection, or calculated stats are missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player body persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            if (!playerBody.IsReady && !string.IsNullOrWhiteSpace(defaultPlayerSpeciesId))
            {
                BodyOperationResult defaultAssignment = playerBody.AssignSpecies(defaultPlayerSpeciesId, restoring: false, "Prototype player default Species");
                if (!defaultAssignment.Succeeded)
                {
                    Debug.LogWarning(defaultAssignment.Message);
                    return;
                }
            }

            playerBodyParticipant = new PlayerBodyPersistenceParticipant(
                playerBody,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(playerBodyParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerBodyParticipant = null;
            }
        }

        private void EnsurePlayerKnowledgeParticipant()
        {
            if (!registerPlayerKnowledge || playerKnowledgeParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null)
            {
                Debug.LogWarning("Person Knowledge persistence participant was not registered because the prototype Person identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Person Knowledge persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            GameObject owner = playerBody == null ? playerIdentityProgression.gameObject : playerBody.gameObject;
            if (playerKnowledge == null)
            {
                playerKnowledge = owner.GetComponent<PersonKnowledgeRuntime>();
            }

            if (playerKnowledge == null)
            {
                Debug.LogWarning("Person Knowledge persistence participant was not registered because PersonKnowledgeRuntime is not authored on the player.");
                return;
            }

            playerKnowledge.Configure(
                GetDefinitionRegistry(),
                playerIdentityProgression.PersonId,
                ResolvePlayerActorId(),
                playerBody == null ? string.Empty : playerBody.ActorBodyId);

            playerKnowledgeParticipant = new PersonKnowledgePersistenceParticipant(
                playerKnowledge,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(playerKnowledgeParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerKnowledgeParticipant = null;
            }
        }

        private void EnsureWorldAuthoritativeHistoryParticipant()
        {
            if (!registerWorldAuthoritativeHistory || worldAuthoritativeHistoryParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Authoritative History persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = ResolvePlayerPersonId();
            AuthoritativeHistory.Configure(
                GetDefinitionRegistry(),
                worldService.WorldId,
                GetPrototypeSocialPersonIds(personId),
                GetKnownBodyIds());
            worldAuthoritativeHistoryParticipant = new AuthoritativeHistoryPersistenceParticipant(
                AuthoritativeHistory,
                GetDefinitionRegistry,
                () => GetPrototypeSocialPersonIds(ResolvePlayerPersonId()),
                GetKnownBodyIds,
                worldService.WorldId);

            RegisterParticipant(worldAuthoritativeHistoryParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldAuthoritativeHistoryParticipant = null;
            }
        }

        private void EnsurePlayerMemoryParticipant()
        {
            if (!registerPlayerMemory || playerMemoryParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null || definitionCatalog == null || worldAuthoritativeHistoryParticipant == null)
            {
                Debug.LogWarning("Person Memory persistence participant requires identity, definitions, and live Authoritative History.");
                return;
            }

            PlayerMemory.Configure(
                playerIdentityProgression.PersonId,
                GetDefinitionRegistry(),
                AuthoritativeHistory,
                GetPrototypeSocialPersonIds(playerIdentityProgression.PersonId));
            GetDefinitionRegistry().TryGet(KnowledgePolicyDefinition.DefaultPolicyId, out KnowledgePolicyDefinition knowledgePolicy);
            memoryMaintenance = new MemoryMaintenanceService(PlayerMemory, knowledgePolicy);
            playerMemoryParticipant = new PersonMemoryPersistenceParticipant(
                PlayerMemory,
                AuthoritativeHistory,
                GetDefinitionRegistry,
                () => GetPrototypeSocialPersonIds(ResolvePlayerPersonId()),
                playerService.PlayerId);

            RegisterParticipant(playerMemoryParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerMemoryParticipant = null;
            }

            knowledgeHistoryFacade = null;
        }

        private void EnsurePlayerProfessionParticipant()
        {
            if (!registerPlayerProfessions || playerProfessionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Person Profession persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            Professions.Configure(GetDefinitionRegistry(), new[] { personId, playerService.PlayerId });
            playerProfessionParticipant = new PersonProfessionPersistenceParticipant(
                Professions,
                GetDefinitionRegistry,
                () => new[] { personId, playerService.PlayerId },
                playerService.PlayerId);

            RegisterParticipant(playerProfessionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerProfessionParticipant = null;
            }
        }

        private void EnsurePlayerProfessionEntryParticipant()
        {
            if (!registerPlayerProfessionEntries || playerProfessionEntryParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Profession Entry persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            Professions.Configure(GetDefinitionRegistry(), new[] { personId, playerService.PlayerId });
            ProfessionEntries.Configure(GetDefinitionRegistry(), Professions, new[] { personId, playerService.PlayerId });
            playerProfessionEntryParticipant = new ProfessionEntryPersistenceParticipant(
                ProfessionEntries,
                GetDefinitionRegistry,
                () => Professions,
                () => new[] { personId, playerService.PlayerId },
                playerService.PlayerId);

            RegisterParticipant(playerProfessionEntryParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerProfessionEntryParticipant = null;
            }
        }

        private void EnsurePlayerInformationSourceParticipant()
        {
            if (!registerWorldInformationSources || playerInformationSourceParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null)
            {
                Debug.LogWarning("Information Source persistence participant was not registered because the prototype Person identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Information Source persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            InformationSources.Configure(GetDefinitionRegistry(), worldService.WorldId);
            playerInformationSourceParticipant = new InformationSourcePersistenceParticipant(
                InformationSources,
                GetDefinitionRegistry,
                worldService.WorldId,
                PersistenceScope.SharedWorld,
                InformationSourcePersistenceParticipant.WorldKey,
                required: true);

            RegisterParticipant(playerInformationSourceParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerInformationSourceParticipant = null;
            }
        }

        private void EnsurePlayerInformationTransferParticipant()
        {
            if (!registerWorldInformationTransfers || playerInformationTransferParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null)
            {
                Debug.LogWarning("Information Transfer persistence participant was not registered because the prototype Person identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Information Transfer persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            InformationTransfers.Configure(GetDefinitionRegistry(), worldService.WorldId);
            playerInformationTransferParticipant = new InformationTransferPersistenceParticipant(
                InformationTransfers,
                GetDefinitionRegistry,
                worldService.WorldId,
                PersistenceScope.SharedWorld,
                InformationTransferPersistenceParticipant.WorldKey,
                required: true);

            RegisterParticipant(playerInformationTransferParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerInformationTransferParticipant = null;
            }
        }

        private void EnsurePlayerTrainingParticipant()
        {
            if (!registerPlayerTraining || playerTrainingParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Training persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            ProfessionEntries.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            InformationTransfers.Configure(GetDefinitionRegistry(), worldService.WorldId);
            Training.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, knownPersons);
            playerTrainingParticipant = new TrainingPersistenceParticipant(
                Training,
                GetDefinitionRegistry,
                () => Professions,
                () => InformationTransfers,
                () => knownPersons,
                playerService.PlayerId);

            RegisterParticipant(playerTrainingParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerTrainingParticipant = null;
            }
        }

        private void EnsurePlayerProfessionalActivityParticipant()
        {
            if (!registerPlayerProfessionalActivities || playerProfessionalActivityParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Professional Activity persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            ProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            playerProfessionalActivityParticipant = new ProfessionalActivityPersistenceParticipant(
                ProfessionalActivities,
                GetDefinitionRegistry,
                () => Professions,
                () => knownPersons,
                playerService.PlayerId);

            RegisterParticipant(playerProfessionalActivityParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerProfessionalActivityParticipant = null;
            }
        }

        private void EnsurePlayerCredentialParticipant()
        {
            if (!registerPlayerCredentials || playerCredentialParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Credential persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            string[] authorities = GetPrototypeCredentialAuthorities();
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            Training.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, knownPersons);
            ProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            Credentials.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, knownPersons, authorities);
            playerCredentialParticipant = new CredentialPersistenceParticipant(
                Credentials,
                GetDefinitionRegistry,
                () => Professions,
                () => Training,
                () => ProfessionalActivities,
                () => knownPersons,
                () => authorities,
                playerService.PlayerId);

            RegisterParticipant(playerCredentialParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerCredentialParticipant = null;
            }
        }

        private void EnsurePlayerProfessionalRankParticipant()
        {
            if (!registerPlayerProfessionalRanks || playerProfessionalRankParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Professional rank persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            string[] authorities = GetPrototypeCredentialAuthorities();
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            Training.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, knownPersons);
            ProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            Credentials.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, knownPersons, authorities);
            ProfessionalRanks.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, knownPersons, authorities);
            playerProfessionalRankParticipant = new ProfessionalRankPersistenceParticipant(
                ProfessionalRanks,
                GetDefinitionRegistry,
                () => Professions,
                () => Training,
                () => ProfessionalActivities,
                () => Credentials,
                () => knownPersons,
                () => authorities,
                playerService.PlayerId);

            RegisterParticipant(playerProfessionalRankParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerProfessionalRankParticipant = null;
            }
        }

        private void EnsurePlayerPositionEmploymentParticipant()
        {
            if (!registerPlayerPositionEmployment || playerPositionEmploymentParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Position employment persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            string[] authorities = GetPrototypeCredentialAuthorities();
            string[] organizations = GetPrototypeOrganizations();
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            Training.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, knownPersons);
            ProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            Credentials.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, knownPersons, authorities);
            ProfessionalRanks.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, knownPersons, authorities);
            PositionEmployment.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, knownPersons, organizations, authorities);
            playerPositionEmploymentParticipant = new PositionEmploymentPersistenceParticipant(
                PositionEmployment,
                GetDefinitionRegistry,
                () => Professions,
                () => Training,
                () => ProfessionalActivities,
                () => Credentials,
                () => ProfessionalRanks,
                () => knownPersons,
                () => organizations,
                () => authorities,
                playerService.PlayerId);

            RegisterParticipant(playerPositionEmploymentParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerPositionEmploymentParticipant = null;
            }
        }

        private void EnsurePlayerCareerHistoryParticipant()
        {
            if (!registerPlayerCareerHistory || playerCareerHistoryParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Career history persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            string[] authorities = GetPrototypeCredentialAuthorities();
            string[] organizations = GetPrototypeOrganizations();
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            Training.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, knownPersons);
            ProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            Credentials.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, knownPersons, authorities);
            ProfessionalRanks.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, knownPersons, authorities);
            PositionEmployment.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, knownPersons, organizations, authorities);
            CareerHistory.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, knownPersons, organizations, authorities);
            playerCareerHistoryParticipant = new CareerHistoryPersistenceParticipant(
                CareerHistory,
                GetDefinitionRegistry,
                () => Professions,
                () => Training,
                () => ProfessionalActivities,
                () => Credentials,
                () => ProfessionalRanks,
                () => PositionEmployment,
                () => knownPersons,
                () => organizations,
                () => authorities,
                playerService.PlayerId);

            RegisterParticipant(playerCareerHistoryParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerCareerHistoryParticipant = null;
            }
        }

        private void EnsurePlayerLifePathParticipant()
        {
            if (!registerPlayerLifePaths || playerLifePathParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Life-path persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = new[] { personId, playerService.PlayerId };
            string[] authorities = GetPrototypeCredentialAuthorities();
            string[] organizations = GetPrototypeOrganizations();
            Professions.Configure(GetDefinitionRegistry(), knownPersons);
            Training.Configure(GetDefinitionRegistry(), Professions, InformationTransfers, knownPersons);
            ProfessionalActivities.Configure(GetDefinitionRegistry(), Professions, knownPersons);
            Credentials.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, knownPersons, authorities);
            ProfessionalRanks.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, knownPersons, authorities);
            PositionEmployment.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, knownPersons, organizations, authorities);
            CareerHistory.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, knownPersons, organizations, authorities);
            LifePaths.Configure(GetDefinitionRegistry(), Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, CareerHistory, knownPersons, organizations);
            playerLifePathParticipant = new LifePathPersistenceParticipant(
                LifePaths,
                GetDefinitionRegistry,
                () => Professions,
                () => Training,
                () => ProfessionalActivities,
                () => Credentials,
                () => ProfessionalRanks,
                () => PositionEmployment,
                () => CareerHistory,
                () => knownPersons,
                () => organizations,
                playerService.PlayerId);

            RegisterParticipant(playerLifePathParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerLifePathParticipant = null;
            }
        }

        private void EnsurePlayerRelationshipParticipant()
        {
            if (!registerPlayerRelationships || playerRelationshipParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Relationship persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            Relationships.Configure(GetDefinitionRegistry(), knownPersons);
            playerRelationshipParticipant = new RelationshipPersistenceParticipant(
                Relationships,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.PlayerId);

            RegisterParticipant(playerRelationshipParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerRelationshipParticipant = null;
            }
        }

        private void EnsurePlayerInterpersonalAttitudeParticipant()
        {
            if (!registerPlayerInterpersonalAttitudes || playerInterpersonalAttitudeParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Interpersonal attitude persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            InterpersonalAttitudes.Configure(GetDefinitionRegistry(), knownPersons);
            playerInterpersonalAttitudeParticipant = new InterpersonalAttitudePersistenceParticipant(
                InterpersonalAttitudes,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.PlayerId);

            RegisterParticipant(playerInterpersonalAttitudeParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerInterpersonalAttitudeParticipant = null;
            }
        }

        private void EnsureWorldReputationParticipant()
        {
            if (!registerWorldReputation || worldReputationParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Reputation persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            Reputation.Configure(GetDefinitionRegistry(), knownPersons);
            worldReputationParticipant = new ReputationPersistenceParticipant(
                Reputation,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldReputationParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldReputationParticipant = null;
            }
        }

        private void EnsureWorldRumorParticipant()
        {
            if (!registerWorldRumors || worldRumorParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Rumor persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            Rumors.Configure(GetDefinitionRegistry(), knownPersons, ResolveKnowledgeRuntimeForPerson, ResolveMemoryRuntimeForPerson);
            worldRumorParticipant = new RumorPersistenceParticipant(
                Rumors,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldRumorParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldRumorParticipant = null;
            }
        }

        private void EnsureWorldSocialInteractionParticipant()
        {
            if (!registerWorldSocialInteractions || worldSocialInteractionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Social Interaction persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            SocialInteractions.Configure(GetDefinitionRegistry(), knownPersons, Relationships, InterpersonalAttitudes, Reputation, Rumors);
            worldSocialInteractionParticipant = new SocialInteractionPersistenceParticipant(
                SocialInteractions,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldSocialInteractionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldSocialInteractionParticipant = null;
            }
        }

        private void EnsureWorldSocialNormParticipant()
        {
            if (!registerWorldSocialNorms || worldSocialNormParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Social Norm persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            SocialNorms.Configure(GetDefinitionRegistry(), knownPersons, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions);
            worldSocialNormParticipant = new SocialNormPersistenceParticipant(
                SocialNorms,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldSocialNormParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldSocialNormParticipant = null;
            }
        }

        private void EnsureWorldSocialNetworkParticipant()
        {
            if (!registerWorldSocialNetworks || worldSocialNetworkParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Social Network persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            SocialNetworks.Configure(GetDefinitionRegistry(), knownPersons, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions, SocialNorms);
            worldSocialNetworkParticipant = new SocialNetworkPersistenceParticipant(
                SocialNetworks,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldSocialNetworkParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldSocialNetworkParticipant = null;
            }
        }

        private void EnsureWorldSocialDecisionParticipant()
        {
            if (!registerWorldSocialDecisions || worldSocialDecisionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Social Decision persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            SocialDecisions.Configure(GetDefinitionRegistry(), knownPersons, SocialInteractions, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialNorms, SocialNetworks, SocialDecisionModifierSourceCollection.Compose(SocialInfluence, SocialEmotions));
            worldSocialDecisionParticipant = new SocialDecisionPersistenceParticipant(
                SocialDecisions,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldSocialDecisionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldSocialDecisionParticipant = null;
            }
        }

        private void EnsureWorldSocialInfluenceParticipant()
        {
            if (!registerWorldSocialInfluence || worldSocialInfluenceParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Social Influence persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            SocialInfluence.Configure(GetDefinitionRegistry(), knownPersons, InterpersonalAttitudes, Reputation, SocialInteractions, new[] { playerKnowledge });
            SocialDecisions.Configure(GetDefinitionRegistry(), knownPersons, SocialInteractions, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialNorms, SocialNetworks, SocialDecisionModifierSourceCollection.Compose(SocialInfluence, SocialEmotions));
            worldSocialInfluenceParticipant = new SocialInfluencePersistenceParticipant(
                SocialInfluence,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldSocialInfluenceParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldSocialInfluenceParticipant = null;
            }
        }

        private void EnsureWorldSocialEmotionParticipant()
        {
            if (!registerWorldSocialEmotions || worldSocialEmotionParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Social Emotion persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            SocialInfluence.Configure(GetDefinitionRegistry(), knownPersons, InterpersonalAttitudes, Reputation, SocialInteractions, new[] { playerKnowledge });
            SocialEmotions.Configure(GetDefinitionRegistry(), knownPersons, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions, SocialNorms, SocialNetworks, SocialInfluence);
            SocialDecisions.Configure(GetDefinitionRegistry(), knownPersons, SocialInteractions, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialNorms, SocialNetworks, SocialDecisionModifierSourceCollection.Compose(SocialInfluence, SocialEmotions));
            worldSocialEmotionParticipant = new SocialEmotionPersistenceParticipant(
                SocialEmotions,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldSocialEmotionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldSocialEmotionParticipant = null;
            }
        }

        private void EnsureWorldFamilyRelationshipParticipant()
        {
            if (!registerWorldFamilyRelationships || worldFamilyRelationshipParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (definitionCatalog == null)
            {
                Debug.LogWarning("Family Relationship persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            string personId = playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService.PlayerId
                : playerIdentityProgression.PersonId;
            string[] knownPersons = GetPrototypeSocialPersonIds(personId);
            FamilyRelationships.Configure(GetDefinitionRegistry(), knownPersons, Relationships, InterpersonalAttitudes, SocialInteractions, playerService.WorldId, GetPrototypeAdultPersonIds(personId));
            worldFamilyRelationshipParticipant = new FamilyRelationshipPersistenceParticipant(
                FamilyRelationships,
                GetDefinitionRegistry,
                () => knownPersons,
                playerService.WorldId);

            RegisterParticipant(worldFamilyRelationshipParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldFamilyRelationshipParticipant = null;
            }
        }

        private void EnsurePlayerInformationAccessParticipant()
        {
            if (!registerWorldInformationAccess || playerInformationAccessParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null)
            {
                Debug.LogWarning("Information Access persistence participant was not registered because the prototype Person identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Information Access persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            InformationAccess.Configure(GetDefinitionRegistry(), worldService.WorldId);
            foreach (InformationAccessPolicyDefinition definition in GetDefinitionRegistry().DefinitionsById.Values
                .OfType<InformationAccessPolicyDefinition>()
                .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                InformationAccessPolicyData policy = definition.CreatePolicyData(
                    new InformationSubjectReferenceData
                    {
                        subjectType = definition.SubjectType == InformationSubjectType.Unknown ? InformationSubjectType.Custom : definition.SubjectType,
                        subjectId = $"policy-template.{definition.Id}",
                        controllingEntityId = worldService.WorldId,
                        tags = new[] { "policy-template", definition.Id }
                    },
                    controllingEntityId: worldService.WorldId);
                InformationAccess.RegisterPolicy(policy, $"information-access.seed.{definition.Id}");
            }
            playerInformationAccessParticipant = new InformationAccessPersistenceParticipant(
                InformationAccess,
                GetDefinitionRegistry,
                worldService.WorldId,
                PersistenceScope.SharedWorld,
                InformationAccessPersistenceParticipant.WorldKey,
                required: true);

            RegisterParticipant(playerInformationAccessParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerInformationAccessParticipant = null;
            }
        }

        private void EnsurePlayerKnowledgeRecordParticipant()
        {
            if (!registerWorldKnowledgeRecords || playerKnowledgeRecordParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerIdentityProgression == null)
            {
                Debug.LogWarning("Knowledge Record persistence participant was not registered because the prototype Person identity/progression component is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Knowledge Record persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            KnowledgeRecords.Configure(GetDefinitionRegistry(), worldService.WorldId);
            playerKnowledgeRecordParticipant = new KnowledgeRecordPersistenceParticipant(
                KnowledgeRecords,
                GetDefinitionRegistry,
                worldService.WorldId,
                PersistenceScope.SharedWorld,
                KnowledgeRecordPersistenceParticipant.WorldKey,
                required: true);

            RegisterParticipant(playerKnowledgeRecordParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerKnowledgeRecordParticipant = null;
            }
        }

        private void ResolvePlayerPersistenceReferences()
        {
            if (playerInventory == null)
            {
                playerInventory = playerRoot == null ? null : playerRoot.GetComponent<PlayerInventory>();
            }

            if (playerEquipment == null && playerInventory != null)
            {
                playerEquipment = playerInventory.GetComponent<PlayerEquipment>();
            }

            if (playerEquipment == null)
            {
                playerEquipment = playerRoot == null ? null : playerRoot.GetComponent<PlayerEquipment>();
            }

            if (playerInventory == null && playerEquipment != null)
            {
                playerInventory = playerEquipment.GetComponent<PlayerInventory>();
            }

            GameObject playerObject = playerRoot == null
                ? playerInventory == null ? playerEquipment == null ? null : playerEquipment.gameObject : playerInventory.gameObject
                : playerRoot.gameObject;
            if (playerObject == null && playerStats != null)
            {
                playerObject = playerStats.gameObject;
            }

            if (playerStats == null)
            {
                playerStats = playerObject == null ? null : playerObject.GetComponent<PlayerStats>();
            }

            if (playerObject == null && playerStats != null)
            {
                playerObject = playerStats.gameObject;
            }

            if (playerAttributes == null)
            {
                playerAttributes = playerObject == null ? null : playerObject.GetComponent<CharacterAttributes>();
            }

            if (playerCalculatedStats == null)
            {
                playerCalculatedStats = playerObject == null ? null : playerObject.GetComponent<CalculatedStatCollection>();
            }

            if (playerSkills == null)
            {
                playerSkills = playerObject == null ? null : playerObject.GetComponent<CharacterSkillCollection>();
            }

            if (playerTraits == null)
            {
                playerTraits = playerObject == null ? null : playerObject.GetComponent<CharacterTraitCollection>();
            }

            if (playerBody == null)
            {
                playerBody = playerObject == null ? null : playerObject.GetComponent<ActorBodyRuntime>();
            }

            if (playerKnowledge == null)
            {
                playerKnowledge = playerObject == null ? null : playerObject.GetComponent<PersonKnowledgeRuntime>();
            }

            if (playerResources == null)
            {
                playerResources = playerObject == null ? null : playerObject.GetComponent<CharacterResourceCollection>();
            }

            if (playerActorLifecycle == null)
            {
                playerActorLifecycle = playerObject == null ? null : playerObject.GetComponent<ActorLifecycleController>();
            }

            if (playerOngoingEffects == null)
            {
                playerOngoingEffects = playerObject == null ? null : playerObject.GetComponent<OngoingEffectService>();
            }

            if (definitionCatalog != null)
            {
                DefinitionRegistry registry = GetDefinitionRegistry();
                CharacterSystemCoordinator character = playerObject == null ? null : playerObject.GetComponent<CharacterSystemCoordinator>();
                if (character != null && !character.IsReady)
                {
                    character.InitializeFromRegistry(registry, restoring: false);
                }
                playerOngoingEffects?.Configure(playerObject == null ? null : playerObject.GetComponent<CharacterSystemCoordinator>());
                playerStats?.RefreshEquipmentModifiers();
                if (playerKnowledge != null && playerIdentityProgression != null)
                {
                    playerKnowledge.Configure(registry, playerIdentityProgression.PersonId, ResolvePlayerActorId(), playerBody == null ? string.Empty : playerBody.ActorBodyId);
                }
            }

            if (playerHealth == null)
            {
                playerHealth = playerObject == null ? null : playerObject.GetComponent<PlayerHealth>();
            }

            if (playerMana == null)
            {
                playerMana = playerObject == null ? null : playerObject.GetComponent<PlayerMana>();
            }

            if (playerStamina == null)
            {
                playerStamina = playerObject == null ? null : playerObject.GetComponent<PlayerStamina>();
            }

            if (statusEffectController == null)
            {
                statusEffectController = playerObject == null ? null : playerObject.GetComponent<StatusEffectController>();
            }

            if (playerQuestLog == null)
            {
                playerQuestLog = playerObject == null ? null : playerObject.GetComponent<PlayerQuestLog>();
            }

            if (playerContractJournal == null)
            {
                playerContractJournal = playerObject == null ? null : playerObject.GetComponent<PlayerContractJournal>();
            }

            if (playerIdentityProgression == null)
            {
                playerIdentityProgression = playerObject == null ? null : playerObject.GetComponent<PlayerIdentityProgression>();
            }

            if (playerRoot == null)
            {
                playerRoot = playerObject == null ? null : playerObject.transform;
            }

            if (playerIdentityProgression != null)
            {
                WorldEntityIdentity worldEntityIdentity = playerRoot == null ? null : playerRoot.GetComponent<WorldEntityIdentity>();
                playerIdentityProgression.ConfigureRuntimeReferences(playerStats, worldEntityIdentity, playTimeTracker, overallLevelConfiguration);
                if (definitionCatalog != null)
                {
                    playerIdentityProgression.ConfigureDefinitions(GetDefinitionRegistry());
                }
            }

            if (playerSkillActionEventSource == null)
            {
                playerSkillActionEventSource = playerObject == null ? null : playerObject.GetComponent<PlayerSkillActionEventSource>();
            }

            if (playerSkillActionEventSource != null && playerObject != null)
            {
                playerSkillActionEventSource.Configure(
                    playerSkills,
                    playerIdentityProgression,
                    playerObject.GetComponent<PlayerMeleeCombat>(),
                    playerObject.GetComponent<PlayerSpellcaster>(),
                    playerEquipment,
                    playTimeTracker);
            }

            if (playerInput == null)
            {
                playerInput = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerInputReader>();
            }

            if (currentPlaceTracker == null && playerRoot != null)
            {
                currentPlaceTracker = playerRoot.GetComponent<CurrentPlaceTracker>();
            }
        }

        private void EnsurePlayerResourcesParticipant()
        {
            if (!registerPlayerResources || playerResourcesParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerResources == null || playerCalculatedStats == null)
            {
                Debug.LogWarning("Player resources persistence participant was not registered because the prototype player resource collection or calculated stats are missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player resources persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            playerResourcesParticipant = new PlayerResourcesPersistenceParticipant(
                playerResources,
                playerIdentityProgression,
                playerCalculatedStats,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(playerResourcesParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerResourcesParticipant = null;
            }
        }

        private void EnsurePlayerActorLifecycleParticipant()
        {
            if (!registerPlayerActorLifecycle || playerActorLifecycleParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerActorLifecycle == null || playerResources == null)
            {
                Debug.LogWarning("Player actor lifecycle persistence participant was not registered because the lifecycle controller or player resources are missing.");
                return;
            }

            playerActorLifecycleParticipant = new PlayerActorLifecyclePersistenceParticipant(
                playerActorLifecycle,
                playerIdentityProgression,
                playerService.PlayerId);

            RegisterParticipant(playerActorLifecycleParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerActorLifecycleParticipant = null;
            }
        }

        private void EnsurePlayerCombatExecutionParticipant()
        {
            if (!registerPlayerCombatExecution || playerCombatExecutionParticipant != null)
            {
                return;
            }

            playerCombatExecutionParticipant = new PlayerCombatExecutionPersistenceParticipant(
                CombatExecution,
                playerService.PlayerId,
                () => playerIdentityProgression == null ? string.Empty : playerIdentityProgression.PersonId);

            RegisterParticipant(playerCombatExecutionParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerCombatExecutionParticipant = null;
            }
        }

        private void EnsurePlayerOngoingEffectsParticipant()
        {
            if (!registerPlayerOngoingEffects || playerOngoingEffectsParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerOngoingEffects == null || playerRoot == null)
            {
                Debug.LogWarning("Player ongoing effects persistence participant was not registered because the ongoing effects playerService or player root is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player ongoing effects persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            CharacterSystemCoordinator character = playerRoot.GetComponent<CharacterSystemCoordinator>();
            playerOngoingEffects.Configure(character);
            playerOngoingEffectsParticipant = new PlayerOngoingEffectsPersistenceParticipant(
                playerOngoingEffects,
                playerRoot.gameObject,
                GetDefinitionRegistry,
                ResolvePlayerActorId,
                playerService.PlayerId);

            RegisterParticipant(playerOngoingEffectsParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerOngoingEffectsParticipant = null;
            }
        }

        private string ResolvePlayerActorId()
        {
            if (playerRoot == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator character = playerRoot.GetComponent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = playerRoot.GetComponent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private void EnsurePlayerStatusEffectsParticipant()
        {
            if (!registerPlayerStatusEffects || statusEffectsParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerStats == null || statusEffectController == null)
            {
                Debug.LogWarning("Player status-effects persistence participant was not registered because player stats or the status controller is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player status-effects persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            statusEffectsParticipant = new PlayerStatusEffectsPersistenceParticipant(
                playerStats,
                statusEffectController,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(statusEffectsParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                statusEffectsParticipant = null;
            }
        }

        private void EnsurePlayerQuestContractParticipant()
        {
            if (!registerPlayerQuestContract || questContractParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerQuestLog == null || playerContractJournal == null || playerInventory == null)
            {
                Debug.LogWarning("Player quest/contract persistence participant was not registered because the prototype player quest log, contract journal, or inventory is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player quest/contract persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            questContractParticipant = new PlayerQuestContractPersistenceParticipant(
                playerQuestLog,
                playerContractJournal,
                playerInventory,
                GetDefinitionRegistry,
                playerService.PlayerId);

            RegisterParticipant(questContractParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                questContractParticipant = null;
            }
        }

        private void EnsurePlayerLocationParticipant()
        {
            if (!registerPlayerLocation || playerLocationParticipant != null)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerRoot == null)
            {
                Debug.LogWarning("Player location persistence participant was not registered because the prototype player root is missing.");
                return;
            }

            if (definitionCatalog == null)
            {
                Debug.LogWarning("Player location persistence participant was not registered because no definition catalog is assigned.");
                return;
            }

            playerLocationParticipant = new PlayerLocationPersistenceParticipant(
                playerRoot,
                GetDefinitionRegistry,
                playerService.PlayerId,
                ResolveSceneKey(),
                defaultSpawnPointId,
                playerInput,
                inventoryScreenController as IPlayerMenuController,
                currentPlaceTracker);

            playerLocationParticipant.LocationFallbackUsed += OnLocationFallbackUsed;
            RegisterParticipant(playerLocationParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                playerLocationParticipant.LocationFallbackUsed -= OnLocationFallbackUsed;
                playerLocationParticipant = null;
            }
        }

        public string BuildPlayerLocationDiagnosticSummary()
        {
            EnsureInitialized();
            return playerLocationParticipant == null
                ? $"Scene: {ResolveSceneKey()}\nPlayer location participant: not registered"
                : playerLocationParticipant.BuildDiagnosticSummary();
        }

        private void OnLocationFallbackUsed(LocationFallbackEventArgs args)
        {
            string message = args == null ? "Player location fallback was used." : args.Message;
            Debug.LogWarning(message);
            PrototypeHudMessageBus.Show(message);
        }

        private void SubscribeDirtyEvents()
        {
            if (dirtyEventsSubscribed)
            {
                return;
            }

            ResolvePlayerPersistenceReferences();
            if (playerInventory != null)
            {
                playerInventory.InventoryChanged += OnMeaningfulRuntimeStateChanged;
            }

            if (playerEquipment != null)
            {
                playerEquipment.EquipmentChanged += OnMeaningfulRuntimeStateChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged += OnVitalsChanged;
            }

            if (playerMana != null)
            {
                playerMana.ManaChanged += OnResourceChanged;
            }

            if (playerStamina != null)
            {
                playerStamina.CommittedStaminaChanged += OnResourceChanged;
            }

            if (playerResources != null)
            {
                playerResources.ResourceChanged += OnPlayerResourceChanged;
                playerResources.ResourceMaximumChanged += OnPlayerResourceMaximumChanged;
                playerResources.ResourcesRestored += OnPlayerResourcesRestored;
            }

            if (playerActorLifecycle != null)
            {
                playerActorLifecycle.DefeatProcessed += OnActorLifecycleChanged;
                playerActorLifecycle.ActorRecovered += OnActorLifecycleChanged;
                playerActorLifecycle.ActorDied += OnActorLifecycleChanged;
                playerActorLifecycle.ActorRevived += OnActorLifecycleChanged;
            }

            if (combatExecutionService != null)
            {
                combatExecutionService.CombatExecutionCommitted += OnCombatExecutionCommitted;
                combatExecutionService.CooldownChanged += OnCombatExecutionChanged;
                combatExecutionService.ExecutionCompleted += OnCombatExecutionChanged;
            }

            if (playerOngoingEffects != null)
            {
                playerOngoingEffects.OngoingEffectApplied += OnOngoingEffectApplicationChanged;
                playerOngoingEffects.OngoingEffectRefreshed += OnOngoingEffectApplicationChanged;
                playerOngoingEffects.OngoingEffectStackChanged += OnOngoingEffectApplicationChanged;
                playerOngoingEffects.OngoingEffectTickProcessed += OnOngoingEffectTickChanged;
                playerOngoingEffects.OngoingEffectTickSkipped += OnOngoingEffectTickChanged;
                playerOngoingEffects.OngoingEffectCancelled += OnOngoingEffectCancellationChanged;
                playerOngoingEffects.OngoingEffectCompleted += OnOngoingEffectCompleted;
            }

            if (statusEffectController != null)
            {
                statusEffectController.StatusAdded += OnStatusChanged;
                statusEffectController.StatusChanged += OnStatusChanged;
                statusEffectController.StatusRemoved += OnStatusChanged;
                statusEffectController.StatusExpired += OnStatusChanged;
            }

            if (playerQuestLog != null)
            {
                playerQuestLog.QuestLogChanged += OnQuestContractChanged;
            }

            if (playerContractJournal != null)
            {
                playerContractJournal.JournalChanged += OnQuestContractChanged;
            }

            if (currentPlaceTracker != null)
            {
                currentPlaceTracker.CurrentPlaceChanged += OnPlaceChanged;
            }

            if (playerIdentityProgression != null)
            {
                playerIdentityProgression.ProgressionChanged += OnIdentityProgressionChanged;
                playerIdentityProgression.OriginAssigned += OnOriginAssigned;
                playerIdentityProgression.BirthGiftAssigned += OnBirthGiftAssigned;
            }

            if (playerAttributes != null)
            {
                playerAttributes.AttributesChanged += OnAttributesChanged;
            }

            if (playerCalculatedStats != null)
            {
                playerCalculatedStats.CalculatedStatsChanged += OnCalculatedStatsChanged;
            }

            if (playerSkills != null)
            {
                playerSkills.SkillsChanged += OnSkillsChanged;
                playerSkills.HiddenProgressChanged += OnSkillHiddenProgressChanged;
            }

            if (playerTraits != null)
            {
                playerTraits.TraitsChanged += OnTraitsChanged;
                playerTraits.TraitRecordChanged += OnTraitRecordChanged;
            }

            if (playerBody != null)
            {
                playerBody.BodyChanged += OnBodyChanged;
            }

            if (playerKnowledge != null)
            {
                playerKnowledge.KnowledgeChanged += OnKnowledgeChanged;
            }

            dirtyEventsSubscribed = true;
        }

        private void UnsubscribeDirtyEvents()
        {
            if (!dirtyEventsSubscribed)
            {
                return;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryChanged -= OnMeaningfulRuntimeStateChanged;
            }

            if (playerEquipment != null)
            {
                playerEquipment.EquipmentChanged -= OnMeaningfulRuntimeStateChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= OnVitalsChanged;
            }

            if (playerMana != null)
            {
                playerMana.ManaChanged -= OnResourceChanged;
            }

            if (playerStamina != null)
            {
                playerStamina.CommittedStaminaChanged -= OnResourceChanged;
            }

            if (playerResources != null)
            {
                playerResources.ResourceChanged -= OnPlayerResourceChanged;
                playerResources.ResourceMaximumChanged -= OnPlayerResourceMaximumChanged;
                playerResources.ResourcesRestored -= OnPlayerResourcesRestored;
            }

            if (playerActorLifecycle != null)
            {
                playerActorLifecycle.DefeatProcessed -= OnActorLifecycleChanged;
                playerActorLifecycle.ActorRecovered -= OnActorLifecycleChanged;
                playerActorLifecycle.ActorDied -= OnActorLifecycleChanged;
                playerActorLifecycle.ActorRevived -= OnActorLifecycleChanged;
            }

            if (combatExecutionService != null)
            {
                combatExecutionService.CombatExecutionCommitted -= OnCombatExecutionCommitted;
                combatExecutionService.CooldownChanged -= OnCombatExecutionChanged;
                combatExecutionService.ExecutionCompleted -= OnCombatExecutionChanged;
            }

            if (playerOngoingEffects != null)
            {
                playerOngoingEffects.OngoingEffectApplied -= OnOngoingEffectApplicationChanged;
                playerOngoingEffects.OngoingEffectRefreshed -= OnOngoingEffectApplicationChanged;
                playerOngoingEffects.OngoingEffectStackChanged -= OnOngoingEffectApplicationChanged;
                playerOngoingEffects.OngoingEffectTickProcessed -= OnOngoingEffectTickChanged;
                playerOngoingEffects.OngoingEffectTickSkipped -= OnOngoingEffectTickChanged;
                playerOngoingEffects.OngoingEffectCancelled -= OnOngoingEffectCancellationChanged;
                playerOngoingEffects.OngoingEffectCompleted -= OnOngoingEffectCompleted;
            }

            if (statusEffectController != null)
            {
                statusEffectController.StatusAdded -= OnStatusChanged;
                statusEffectController.StatusChanged -= OnStatusChanged;
                statusEffectController.StatusRemoved -= OnStatusChanged;
                statusEffectController.StatusExpired -= OnStatusChanged;
            }

            if (playerIdentityProgression != null)
            {
                playerIdentityProgression.ProgressionChanged -= OnIdentityProgressionChanged;
                playerIdentityProgression.OriginAssigned -= OnOriginAssigned;
                playerIdentityProgression.BirthGiftAssigned -= OnBirthGiftAssigned;
            }

            if (playerAttributes != null)
            {
                playerAttributes.AttributesChanged -= OnAttributesChanged;
            }

            if (playerCalculatedStats != null)
            {
                playerCalculatedStats.CalculatedStatsChanged -= OnCalculatedStatsChanged;
            }

            if (playerSkills != null)
            {
                playerSkills.SkillsChanged -= OnSkillsChanged;
                playerSkills.HiddenProgressChanged -= OnSkillHiddenProgressChanged;
            }

            if (playerTraits != null)
            {
                playerTraits.TraitsChanged -= OnTraitsChanged;
                playerTraits.TraitRecordChanged -= OnTraitRecordChanged;
            }

            if (playerBody != null)
            {
                playerBody.BodyChanged -= OnBodyChanged;
            }

            if (playerKnowledge != null)
            {
                playerKnowledge.KnowledgeChanged -= OnKnowledgeChanged;
            }

            if (playerQuestLog != null)
            {
                playerQuestLog.QuestLogChanged -= OnQuestContractChanged;
            }

            if (playerContractJournal != null)
            {
                playerContractJournal.JournalChanged -= OnQuestContractChanged;
            }

            if (currentPlaceTracker != null)
            {
                currentPlaceTracker.CurrentPlaceChanged -= OnPlaceChanged;
            }

            dirtyEventsSubscribed = false;
        }

        private void OnMeaningfulRuntimeStateChanged()
        {
            dirtyTracker?.MarkDirty("Player state changed.");
        }

        private void OnQuestContractChanged()
        {
            dirtyTracker?.MarkDirty("Quest or contract state changed.");
            autosaveCoordinator?.RequestAutosave("Progression");
            knowledgeHistoryEventBridge?.RecordQuestState(playerQuestLog?.Quests.Select(quest => quest?.Definition?.QuestId));
        }

        private void OnVitalsChanged(int current, int maximum)
        {
            dirtyTracker?.MarkDirty("Player vitals changed.");
        }

        private void OnResourceChanged(float current, float maximum)
        {
            dirtyTracker?.MarkDirty("Player resource changed.");
        }

        private void OnPlayerResourceChanged(CharacterResourceCollection resources, ResourceChangeResult result)
        {
            if (result == null || result.Request.Restoration || result.Request.Migration)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player resource changed.");
        }

        private void OnActorLifecycleChanged(ActorLifecycleResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player actor lifecycle changed.");
            if (result.ResultingState == ActorLifecycleState.Dead)
            {
                knowledgeHistoryEventBridge?.RecordDeath(result.TransactionId, result.Trigger.ToString());
            }
            else if (result.Transition == LifecycleTransitionKind.Revival)
            {
                knowledgeHistoryEventBridge?.RecordRevival(result.TransactionId);
            }
        }

        private void OnCombatExecutionCommitted(CombatExecutionCommitted committed)
        {
            if (committed == null)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player combat execution changed.");
        }

        private void OnCombatExecutionChanged(CombatExecutionResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player combat execution changed.");
        }

        private void OnOngoingEffectApplicationChanged(OngoingEffectApplicationResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player ongoing effect changed.");
        }

        private void OnOngoingEffectTickChanged(OngoingEffectTickResult result)
        {
            if (result == null || string.Equals(result.Code, OngoingEffectResultCode.DuplicateTick, System.StringComparison.Ordinal))
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player ongoing effect tick processed.");
        }

        private void OnOngoingEffectCancellationChanged(OngoingEffectCancellationResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player ongoing effect cancelled.");
        }

        private void OnOngoingEffectCompleted(RuntimeOngoingEffectInstance instance)
        {
            dirtyTracker?.MarkDirty("Player ongoing effect completed.");
        }

        private void OnPlayerResourceMaximumChanged(CharacterResourceCollection resources, ResourceSnapshot snapshot, float oldMaximum, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player resource maximum changed.");
        }

        private void OnPlayerResourcesRestored(CharacterResourceCollection resources, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player resources changed.");
        }

        private void OnStatusChanged(RuntimeStatusEffect status)
        {
            dirtyTracker?.MarkDirty("Status effect state changed.");
        }

        private void OnPlaceChanged(PlaceDefinition place, bool entered)
        {
            dirtyTracker?.MarkDirty("Player location changed.");
            knowledgeHistoryEventBridge?.RecordLocation(place == null ? string.Empty : place.Id, entered);
        }

        private void OnIdentityProgressionChanged(PlayerIdentityProgression progression, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player identity/progression changed.");
        }

        private void OnOriginAssigned(PlayerIdentityProgression progression, RuntimeOriginAssignmentRecord origin, bool restoring)
        {
            if (!restoring)
            {
                knowledgeHistoryEventBridge?.RecordIdentityAssignment($"origin.{origin?.originId}", origin?.originId, progression?.BirthGift?.giftDefinitionId);
            }
        }

        private void OnBirthGiftAssigned(PlayerIdentityProgression progression, RuntimeBirthGiftRecord gift, bool restoring)
        {
            if (!restoring)
            {
                knowledgeHistoryEventBridge?.RecordIdentityAssignment($"birth-gift.{gift?.giftDefinitionId}", progression?.Origin?.originId, gift?.giftDefinitionId);
            }
        }

        private void OnAttributesChanged(CharacterAttributes attributes, IReadOnlyList<string> attributeIds, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player attributes changed.");
        }

        private void OnCalculatedStatsChanged(CalculatedStatCollection stats, IReadOnlyList<string> statIds, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player calculated stats changed.");
        }

        private void OnSkillsChanged(CharacterSkillCollection skills, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player Skills changed.");
        }

        private void OnSkillHiddenProgressChanged(CharacterSkillCollection skills, SkillLearningProgressRecord progress, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player hidden Skill learning progress changed.");
        }

        private void OnTraitsChanged(CharacterTraitCollection traits, TraitOperationResult result, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player Traits changed.");
        }

        private void OnTraitRecordChanged(CharacterTraitCollection traits, RuntimeTraitRecord record, bool restoring)
        {
            if (restoring)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player Trait record changed.");
        }

        private void OnBodyChanged(ActorBodyRuntime bodyRuntime, BodyOperationResult result, bool restoring)
        {
            if (restoring || result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player body Species changed.");
            knowledgeHistoryEventBridge?.RecordBodyTransition(result.Snapshot?.ActorBodyId, result.Message);
            if (playerKnowledge != null && result.Snapshot != null)
            {
                foreach (KnowledgeBeliefRecord belief in playerKnowledge.CreateSnapshot(bodyId: result.Snapshot.ActorBodyId).Beliefs)
                {
                    playerKnowledge.MarkStale(belief.BeliefId, $"knowledge.body-stale.{result.Snapshot.ActorBodyId}.{result.Snapshot.BodyRevision}.{belief.BeliefId}", "Body-specific Knowledge marked stale after body change.");
                }
            }
        }

        private void OnKnowledgeChanged(PersonKnowledgeRuntime runtime, KnowledgeOperationResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            dirtyTracker?.MarkDirty("Player Knowledge changed.");
            knowledgeHistoryEventBridge?.RecordDiscovery(result);
        }

        private string ResolveSceneKey()
        {
            if (!string.IsNullOrWhiteSpace(sceneKey))
            {
                return sceneKey;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.name)
                ? $"scene.{activeScene.name.ToLowerInvariant()}"
                : "scene.unknown";
        }

        private DefinitionRegistry GetDefinitionRegistry()
        {
            if (definitionCatalog == null)
            {
                return null;
            }

            if (definitionRegistry != null)
            {
                return definitionRegistry;
            }

            DefinitionRegistry catalogRegistry = definitionCatalog.CreateRegistry();
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            HashSet<string> definitionIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (IGameDefinition definition in catalogRegistry.DefinitionsById.Values)
            {
                if (definition != null && definitionIds.Add(definition.Id))
                {
                    definitions.Add(definition);
                }
            }

            definitionRegistry = PrototypeFamilyRelationshipDefinitionFactory.AddMissingPrototypeFamilyRelationshipDefinitions(
                PrototypeSocialEmotionDefinitionFactory.AddMissingPrototypeSocialEmotionDefinitions(
                    PrototypeSocialInfluenceDefinitionFactory.AddMissingPrototypeSocialInfluenceDefinitions(
                        PrototypeSocialDecisionDefinitionFactory.AddMissingPrototypeSocialDecisionDefinitions(
                            PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                                PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                                    PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                                        PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                                            PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                                                PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                                    PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(
                                                        PrototypeProfessionDefinitionFactory.AddMissingPrototypeProfessionDefinitions(
                                                            PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(
                                                                PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(
                                                                    PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(
                                                                        PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                                                                            PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(new DefinitionRegistry(definitions))))))))))))))))));
            definitionRegistry = PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(definitionRegistry);
            definitionRegistry = PrototypeDiplomacyDefinitionFactory.AddMissingPrototypeDiplomacyDefinitions(definitionRegistry);
            definitionRegistry = PrototypeGovernmentDefinitionFactory.AddMissingPrototypeGovernmentDefinitions(definitionRegistry);
            definitionRegistry = PrototypeLegalDefinitionFactory.AddMissingPrototypeLegalDefinitions(definitionRegistry);
            definitionRegistry = PrototypeCrimeDefinitionFactory.AddMissingPrototypeCrimeDefinitions(definitionRegistry);
            definitionRegistry = PrototypeJusticeDefinitionFactory.AddMissingPrototypeJusticeDefinitions(definitionRegistry);
            definitionRegistry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(definitionRegistry);
            definitionRegistry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(definitionRegistry);
            definitionRegistry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(definitionRegistry);
            definitionRegistry = PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(definitionRegistry);
            definitionRegistry = PrototypeTravelConditionDefinitionFactory.AddMissingPrototypeTravelConditionDefinitions(definitionRegistry);
            definitionRegistry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(definitionRegistry);
            definitionRegistry = PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(definitionRegistry);
            definitionRegistry = PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(definitionRegistry);
            definitionRegistry = PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(definitionRegistry);
            definitionRegistry = PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(definitionRegistry);
            definitionRegistry = PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(definitionRegistry);
            definitionRegistry = PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(definitionRegistry);
            return definitionRegistry;
        }

        private PersonKnowledgeRuntime ResolveKnowledgeRuntimeForPerson(string personId)
        {
            if (playerKnowledge == null || playerIdentityProgression == null)
            {
                return null;
            }

            return string.Equals(playerIdentityProgression.PersonId, personId, System.StringComparison.Ordinal)
                ? playerKnowledge
                : null;
        }

        private string ResolvePlayerPersonId()
        {
            return playerIdentityProgression == null || string.IsNullOrWhiteSpace(playerIdentityProgression.PersonId)
                ? playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId
                : playerIdentityProgression.PersonId;
        }

        private IEnumerable<string> GetKnownBodyIds()
        {
            string bodyId = playerBody == null ? string.Empty : playerBody.ActorBodyId;
            return string.IsNullOrWhiteSpace(bodyId) ? Array.Empty<string>() : new[] { bodyId };
        }

        private void EnsureKnowledgeHistoryRuntimesConfigured()
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            string personId = ResolvePlayerPersonId();
            string worldId = worldService == null ? PersistenceService.LocalWorldId : worldService.WorldId;
            string[] people = GetPrototypeSocialPersonIds(personId);

            AuthoritativeHistory.Configure(registry, worldId, people, GetKnownBodyIds());
            PlayerMemory.Configure(personId, registry, AuthoritativeHistory, people);
            InformationSources.Configure(registry, worldId);
            InformationTransfers.Configure(registry, worldId);
            InformationAccess.Configure(registry, worldId);
            KnowledgeRecords.Configure(registry, worldId);
        }

        private KnowledgeHistoryRuntimeSet CreateKnowledgeHistoryRuntimeSet()
        {
            return new KnowledgeHistoryRuntimeSet
            {
                DefinitionRegistry = GetDefinitionRegistry(),
                PersonId = ResolvePlayerPersonId(),
                WorldId = worldService == null ? PersistenceService.LocalWorldId : worldService.WorldId,
                KnownPersonIds = GetPrototypeSocialPersonIds(ResolvePlayerPersonId()),
                KnownBodyIds = GetKnownBodyIds().ToArray(),
                KnowledgeRuntime = playerKnowledge,
                HistoryRuntime = AuthoritativeHistory,
                MemoryRuntime = PlayerMemory,
                SourceRuntime = InformationSources,
                TransferRuntime = InformationTransfers,
                AccessRuntime = InformationAccess,
                RecordRuntime = KnowledgeRecords
            };
        }

        public ObservationAuthoritySnapshot CreatePlayerObservationAuthority()
        {
            CharacterCapabilityCollection capabilities = playerBody == null ? null : playerBody.GetComponent<CharacterCapabilityCollection>();
            string[] equipmentTags = (playerEquipment?.Slots ?? Array.Empty<EquipmentSlotState>())
                .Where(slot => slot != null && !slot.IsEmpty && slot.Item != null)
                .SelectMany(slot => slot.Item.Tags)
                .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
                .Select(tag => tag.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            string[] knowledgeDomains = (playerKnowledge?.CreateSnapshot().Beliefs ?? Array.Empty<KnowledgeBeliefRecord>())
                .Where(belief => belief.Definition != null && belief.State != KnowledgeBeliefState.Unknown && belief.State != KnowledgeBeliefState.Forgotten)
                .Select(belief => $"domain.{belief.Definition.Domain.ToString().ToLowerInvariant()}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return new ObservationAuthoritySnapshot
            {
                CapabilityIds = capabilities?.GetSnapshots()
                    .Where(snapshot => snapshot.BooleanValue && !snapshot.Blocked)
                    .Select(snapshot => snapshot.CapabilityId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<string>(),
                TraitIds = playerTraits?.GetActiveTraits()
                    .Where(snapshot => snapshot?.Definition != null)
                    .Select(snapshot => snapshot.Definition.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<string>(),
                SkillIds = playerSkills?.LearnedSkills
                    .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.skillDefinitionId))
                    .Select(skill => skill.skillDefinitionId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<string>(),
                EquipmentTagIds = equipmentTags,
                KnowledgeDomainIds = knowledgeDomains,
                ExpertiseQuality = playerSkills == null || playerSkills.LearnedSkills.Count == 0 ? KnowledgeConfidence.DefaultObservation : 700,
                ToolQuality = equipmentTags.Length == 0 ? KnowledgeConfidence.DefaultObservation : 700
            };
        }

        private string[] GetKnownPlaceIds()
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            return registry?.DefinitionsById.Values
                .OfType<PlaceDefinition>()
                .Select(definition => definition.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        private PersonMemoryRuntime ResolveMemoryRuntimeForPerson(string personId)
        {
            return string.Equals(ResolvePlayerPersonId(), personId, StringComparison.Ordinal)
                ? PlayerMemory
                : null;
        }

        private string[] GetPrototypeSocialPersonIds(string primaryPersonId)
        {
            return new[]
            {
                primaryPersonId,
                playerService == null ? PersistenceService.LocalPlayerId : playerService.PlayerId,
                "person.prototype.npc",
                "person.prototype.friend",
                "person.prototype.rival",
                "person.prototype.parent",
                "person.prototype.child",
                "person.prototype.dependent",
                "person.prototype.partner",
                "person.prototype.spouse",
                "person.prototype.sibling",
                "person.prototype.cousin",
                "person.prototype.mentor",
                "person.prototype.student"
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(System.StringComparer.Ordinal)
                .ToArray();
        }

        private string[] GetPrototypeAdultPersonIds(string primaryPersonId)
        {
            return GetPrototypeSocialPersonIds(primaryPersonId)
                .Where(value => !value.Contains(".child", System.StringComparison.Ordinal) && !value.Contains(".dependent", System.StringComparison.Ordinal))
                .ToArray();
        }

        private static string[] GetPrototypeCredentialAuthorities()
        {
            return new[]
            {
                "authority.guild.prototype",
                "authority.medical.prototype",
                "organization.prototype.guild",
                "authority.government.prototype",
                "authority.school.prototype",
                PrototypeProfessionDefinitionFactory.PositionAppointAuthorityId,
                PrototypeProfessionDefinitionFactory.PositionDutyAssignAuthorityId,
                PrototypeProfessionDefinitionFactory.PositionSuperviseAuthorityId,
                PrototypeProfessionDefinitionFactory.PositionRestrictedRecordsAuthorityId,
                PrototypeProfessionDefinitionFactory.BlacksmithTeachPermissionId,
                PrototypeProfessionDefinitionFactory.ForgeRestrictedStationPermissionId,
                "organization.prototype.royal-forge",
                "organization.prototype.temple",
                "organization.prototype.university",
                "organization.prototype.government",
                "organization.prototype.independent",
                PersistenceService.LocalPlayerId
            };
        }

        private static string[] GetPrototypeOrganizations()
        {
            return PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds
                .Concat(new[] { PersistenceService.LocalPlayerId })
                .ToArray();
        }
    }
}
