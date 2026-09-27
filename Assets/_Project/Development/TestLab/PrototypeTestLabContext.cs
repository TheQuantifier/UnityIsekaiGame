#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Development
{
    public sealed class PrototypeTestLabContext
    {
        public DefinitionCatalog DefinitionCatalog;
        public PlayerInventory Inventory;
        public PlayerEquipment Equipment;
        public PlayerStats PlayerStats;
        public PlayerHealth PlayerHealth;
        public PlayerMana PlayerMana;
        public PlayerStamina PlayerStamina;
        public CharacterAttributes PlayerAttributes;
        public CalculatedStatCollection PlayerCalculatedStats;
        public CharacterResourceCollection PlayerResources;
        public ActorLifecycleController PlayerLifecycle;
        public CombatStateService CombatState;
        public OngoingEffectService PlayerOngoingEffects;
        public CharacterSkillCollection PlayerSkills;
        public CharacterTraitCollection PlayerTraits;
        public PersonKnowledgeRuntime PlayerKnowledge;
        public InformationAccessRuntime InformationAccess;
        public KnowledgeRecordRuntime KnowledgeRecords;
        public CharacterSystemCoordinator CharacterSystem;
        public StatusEffectController PlayerStatuses;
        public PlayerIdentityProgression IdentityProgression;
        public PlayerSpellcaster Spellcaster;
        public PlayerSpellLoadout SpellLoadout;
        public PrototypeNarrativeCoordinator Narrative;
        public PrototypeTestController TestController;
        public PrototypePersistenceServiceBehaviour Persistence;
        public Transform PlayerTransform;
        public EnemyHealth EnemyHealth;
        public PrototypeEnemyController EnemyController;
        public EnemyMeleeAttack EnemyAttack;
        public ActorLifecycleController EnemyLifecycle;
        public OngoingEffectService EnemyOngoingEffects;
        public StatusEffectController EnemyStatuses;
        public Transform EnemyTransform;
    }
}
#endif
