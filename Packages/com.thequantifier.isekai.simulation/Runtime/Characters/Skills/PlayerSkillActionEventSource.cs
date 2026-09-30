using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Skills
{
    public sealed class PlayerSkillActionEventSource : MonoBehaviour
    {
        [SerializeField] private CharacterSkillCollection skills;
        [SerializeField] private PlayerIdentityProgression identity;
        [SerializeField] private PlayerMeleeCombat meleeCombat;
        [SerializeField] private PlayerSpellcaster spellcaster;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayTimeTracker playTimeTracker;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (meleeCombat != null)
            {
                meleeCombat.AttackResolved += OnAttackResolved;
            }

            if (spellcaster != null)
            {
                spellcaster.SpellCastResolved += OnSpellCastResolved;
            }
        }

        private void OnDisable()
        {
            if (meleeCombat != null)
            {
                meleeCombat.AttackResolved -= OnAttackResolved;
            }

            if (spellcaster != null)
            {
                spellcaster.SpellCastResolved -= OnSpellCastResolved;
            }
        }

        public void Configure(
            CharacterSkillCollection skillCollection,
            PlayerIdentityProgression identityProgression,
            PlayerMeleeCombat playerMeleeCombat,
            PlayerSpellcaster playerSpellcaster,
            PlayerEquipment playerEquipment,
            PlayTimeTracker tracker)
        {
            if (meleeCombat != null)
            {
                meleeCombat.AttackResolved -= OnAttackResolved;
            }

            if (spellcaster != null)
            {
                spellcaster.SpellCastResolved -= OnSpellCastResolved;
            }

            skills = skillCollection == null ? skills : skillCollection;
            identity = identityProgression == null ? identity : identityProgression;
            meleeCombat = playerMeleeCombat == null ? meleeCombat : playerMeleeCombat;
            spellcaster = playerSpellcaster == null ? spellcaster : playerSpellcaster;
            equipment = playerEquipment == null ? equipment : playerEquipment;
            playTimeTracker = tracker == null ? playTimeTracker : tracker;

            if (isActiveAndEnabled)
            {
                if (meleeCombat != null)
                {
                    meleeCombat.AttackResolved += OnAttackResolved;
                }

                if (spellcaster != null)
                {
                    spellcaster.SpellCastResolved += OnSpellCastResolved;
                }
            }
        }

        private void ResolveReferences()
        {
            skills ??= GetComponent<CharacterSkillCollection>();
            identity ??= GetComponent<PlayerIdentityProgression>();
            meleeCombat ??= GetComponent<PlayerMeleeCombat>();
            spellcaster ??= GetComponent<PlayerSpellcaster>();
            equipment ??= GetComponent<PlayerEquipment>();
            playTimeTracker ??= UnityEngine.Object.FindAnyObjectByType<PlayTimeTracker>();
        }

        private void OnAttackResolved(MeleeAttackResult result)
        {
            if (skills == null || !result.Started)
            {
                return;
            }

            EquipmentSlotState mainHand = equipment == null ? null : equipment.GetSlot(EquipmentSlotType.MainHand);
            ItemDefinition item = mainHand == null ? null : mainHand.Item;
            string itemInstanceId = mainHand == null ? string.Empty : mainHand.ItemInstanceId;
            SkillActionEventCategory category = item == null ? SkillActionEventCategory.UnarmedAttack : SkillActionEventCategory.PhysicalWeaponAction;

            skills.RecordQualifyingAction(new SkillActionExecutionEvent
            {
                EventId = CreateEventId("melee"),
                ActorId = identity == null ? string.Empty : identity.PlayerId,
                ActionDefinitionId = item == null ? "action.unarmed-attack" : "action.physical-weapon-attack",
                ActionCategory = category,
                EquipmentItemInstanceId = itemInstanceId,
                ItemDefinitionId = item == null ? string.Empty : item.Id,
                ItemCategory = item == null ? null : item.PrimaryCategory,
                ItemTags = item == null ? Array.Empty<TagDefinition>() : item.Tags,
                Executed = true,
                IntendedResultSucceeded = result.HitTarget,
                PlaytimeSeconds = playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds,
                SourceSystem = "player-melee-combat",
                ServerAuthoritative = false
            });
        }

        private void OnSpellCastResolved(SpellDefinition spell, SpellCastResult result)
        {
            if (skills == null || spell == null || !result.Succeeded)
            {
                return;
            }

            skills.RecordQualifyingAction(new SkillActionExecutionEvent
            {
                EventId = CreateEventId("spell"),
                ActorId = identity == null ? string.Empty : identity.PlayerId,
                ActionDefinitionId = spell.Ability == null ? spell.Id : spell.Ability.Id,
                ActionCategory = SkillActionEventCategory.SpellCast,
                ActionTags = spell.Ability == null ? Array.Empty<TagDefinition>() : spell.Ability.Tags,
                MagicTags = spell.Tags.Concat(spell.Ability == null ? Array.Empty<TagDefinition>() : spell.Ability.Tags).ToList(),
                Executed = true,
                IntendedResultSucceeded = true,
                PlaytimeSeconds = playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds,
                SourceSystem = "player-spellcaster",
                ServerAuthoritative = false
            });
        }

        private static string CreateEventId(string kind)
        {
            return $"skill-action.{kind}.{Guid.NewGuid():N}".ToLowerInvariant();
        }
    }
}
