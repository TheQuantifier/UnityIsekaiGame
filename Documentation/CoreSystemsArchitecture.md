# Core Systems Architecture

This document records the prototype architecture through the Step 3 game-data and world-taxonomy closeout. It is practical project documentation, not final game design or lore.

## Step 2 System Overview

The prototype is a first-person Unity 6.5 URP RPG sandbox. The current scene validates movement, interaction, inventory, item use, vitals, equipment, combat, enemy behavior, loot, abilities, statuses, typed damage, spell loadout, dialogue, contracts, quests, HUD feedback, reset controls, NPC person identity, places, and factions.

The major rule is separation between static definitions and runtime state. ScriptableObjects describe authored data. MonoBehaviours and runtime instances own player/enemy/session state.

## Static Definitions

Static data lives in ScriptableObjects and should not be mutated during play:

- `ItemDefinition`: stable item ID, display text, icon, static rarity, stack rules, equipment data, use effects.
- `RarityDefinition`: static scarcity or narrative-significance metadata.
- `QualityDefinition`: possible craftsmanship or generated-excellence levels for future item instances.
- `ConditionDefinition`: named normalized physical-state bands for future item instances.
- `ItemUseEffect` assets: reusable item-use behavior configuration such as health restoration amount.
- `SpellDefinition`: spell ID/name, mana cost, cooldown, range, projectile prefab, damage.
- `DialogueNodeDefinition`: authored dialogue text, speaker fallback, choices, next nodes, portrait fallback.
- `ContractDefinition`: contract ID, title, requester text, objectives, rewards.
- `ContractObjectiveDefinition`: collect, delivery, defeat, and reusable objective configuration.
- `QuestDefinition`: quest ID, giver, category, stages, prerequisites, rewards.
- `QuestStageDefinition`: ordered quest stage metadata and objective definitions.
- `TalkObjectiveDefinition` and `ReachLocationObjectiveDefinition`: quest objective configuration.
- `ContractRewardDefinition`: item reward configuration shared by contracts and quests.
- `BeingDefinition`: stable being type, broad classification, intelligence/social/locomotion metadata, and optional default profile reference.
- `ActorProfileDefinition`: stable reusable base actor configuration for stats and being reference.
- `PlaceDefinition`: stable place identity, hierarchy, classification, optional scene/map metadata, and future discovery/control placeholders.
- `FactionDefinition`: stable faction or organization identity, kind, hierarchy, authority metadata, place/person references, and taxonomy.
- `DamageTypeDefinition`: stable damage identity, hierarchy, Defense policy, and presentation metadata.
- `PersonDefinition`: stable person ID, display name, title, role tags, optional being/profile/place/faction references, metadata placeholders.
- `PlayerMovementSettings`: first-person movement tuning.

Static definitions may reference other definitions, but they must not store inventory quantities, current health, objective progress, reward claim state, cooldowns, dialogue progress, active registry data, or spawned-world state.

## Runtime State Owners

Runtime state is owned by components or runtime instances:

- `PlayerInputReader` owns input action queues and gameplay-blocking flags.
- `FirstPersonCharacterMotor` and `FirstPersonCameraLook` consume abstracted input and never own UI state.
- `CameraInteractionDetector` owns the current raycast target; interactables own their own behavior.
- `PlayerInventory` owns slot contents and quantities.
- `InventorySlot` is runtime slot state, not static item data.
- `ItemInstanceIdentityRuntime` owns item-instance identity, lifecycle, location, ownership, custody, and stack quantity; inventory/equipment slots carry the authoritative instance ID projection, while composition, quality/affixes, and durability remain in their dedicated runtimes.
- `PlayerEquipment` owns equipped items by equipment slot.
- `ActorStats` owns generic runtime stat calculation for combat-capable actors.
- `RuntimeResistanceCollection` is owned per actor by `ActorStats` and calculates typed resistance from profile, equipment, statuses, and future source-owned modifiers.
- `PlayerStats` derives from `ActorStats` and adds player equipment modifier integration.
- `PlayerHealth`, `PlayerStamina`, and `PlayerMana` own current vital values and resource timing.
- `PlayerMeleeCombat`, `PlayerSpellcaster`, and projectile instances own combat/cooldown state.
- `EnemyHealth`, `PrototypeEnemyController`, and `EnemyMeleeAttack` own enemy runtime combat state and read combat stats from `ActorStats` where available.
- `EnemyLootDrop` owns whether the current enemy defeat has already rolled loot.
- `DialogueController` owns the active dialogue node and active participant display override.
- `ContractInstance` owns contract progress, completion, abandonment, and reward claim state.
- `QuestInstance` owns quest state, current stage, current objective instances, and reward claim state.
- `PlayerContractJournal` and `PlayerQuestLog` own active runtime collections for contracts and quests.
- `PersonIdentity` represents a currently loaded scene presence for a static `PersonDefinition`.
- `PlaceIdentity` can represent a loaded scene presence for a static `PlaceDefinition`, but it is not a static registry or save-data source.
- `PersonRegistry` owns only currently loaded active person identities.
- `PersistenceService` owns save-file orchestration, slot paths, envelope serialization, atomic writes, validation, backup reporting, and participant ordering. Persistence participants own their own DTO capture and restore preparation.

## Stable ID Conventions

Stable IDs identify authored definitions and objective targets. They must not be derived from GameObject names.

Step 3.1 adds the shared `IGameDefinition` contract, reusable ID validation, explicit `DefinitionCatalog` assets, and `DefinitionRegistry` lookup. Step 3.2 adds registered `CategoryDefinition` and `TagDefinition` assets plus opt-in classification interfaces for metadata-only category/tag assignment. Step 3.3 adds object/item taxonomy interfaces and item capability validation while keeping stack, use, and equipment behavior data-driven. Step 3.4 adds distinct rarity, quality, and condition definitions, optional static item rarity, and standalone runtime item-instance metadata. Step 3.5 adds runtime item-instance identity, save-data DTOs, restoration through `DefinitionRegistry`, and future stack-compatibility policy. Step 3.6 integrates item instances into inventory/equipment runtime state. Step 3.7 adds shared `AbilityDefinition` and `EffectDefinition` foundations while keeping cooldowns, resources, targets, and active projectiles in runtime owners. Step 3.8 adds runtime status effects and exact-source stat modifiers. Step 3.9 adds generic actor stats and shared combat stat integration. Step 3.10 adds `BeingDefinition` and `ActorProfileDefinition` while keeping person identity and runtime actor state separate. Step 3.11 adds `PlaceDefinition` and typed reach-location references while keeping scenes, triggers, and runtime world state separate from static place identity. Step 3.12 adds `FactionDefinition` and organization metadata while keeping memberships, ranks, reputation, law, diplomacy, and economy as future runtime systems. Step 3.13 adds `DamageTypeDefinition`, typed damage packets, and per-actor typed resistance while keeping health, stats, equipment, and statuses as separate runtime owners. Step 3.14 closes out the architecture with integration documentation, regression checklist coverage, and catalog-level integration tests. See `Documentation/Step3GameDataAndWorldTaxonomyArchitecture.md` and `Documentation/Step3RegressionChecklist.md` for the Step 3 closeout map.

Step 4.1 adds the shared `PersistenceService`, versioned save envelope, participant registration model, JSON payload strategy, slot metadata, atomic local file writes, one-slot backup behavior, validation-only reads, and a prototype-only persistence participant. It does not migrate major gameplay systems yet. See `Documentation/PersistenceServiceFoundation.md` and `Documentation/Step4PersistenceRoadmap.md`.

Current ID families:

- Item IDs: `ItemDefinition.ItemId`
- Spell IDs: `SpellDefinition`
- Contract IDs: `ContractDefinition.ContractId`
- Quest IDs: `QuestDefinition.QuestId`
- Quest stage IDs: `QuestStageDefinition.StageId`
- Person IDs: `PersonDefinition.PersonId`
- Faction IDs: `FactionDefinition.FactionId`
- Talk objective IDs: `TalkObjectiveDefinition.TalkTargetId`, preferably resolved from `PersonDefinition`
- Location IDs: `ReachLocationObjectiveDefinition.TargetPlace` / `TargetLocationId` and `QuestReachLocationReporter.TargetPlace`, with legacy string fallback.
- Contract delivery destination IDs: `DeliveryObjectiveDefinition.DestinationId`
- Defeat target categories: `ContractObjectiveTarget.TargetCategory`

The current code safely rejects empty quest/contract IDs when starting entries. Person identities warn when missing or duplicated while active in the loaded scene.

## Identity Migration

`PersonDefinition` is now the authoritative identity source for NPC people. `Prototype Dialogue NPC` has a `PersonIdentity` assigned to `PrototypeNpcPerson`.

Dialogue and quests use person identity where practical:

- `NpcDialogueInteractable` gets prompt/speaker display from `PersonIdentity`.
- `QuestDialogueTargetReporter` reports Talk objectives using `PersonIdentity.PersonId`.
- `QuestDefinition` can reference a `PersonDefinition` quest giver.
- `TalkObjectiveDefinition` can reference a `PersonDefinition` target.

The old string fields remain as serialized fallback paths:

- `QuestDefinition.questGiverId`
- `QuestDefinition.questGiverDisplayName`
- `TalkObjectiveDefinition.talkTargetId`
- `QuestDialogueTargetReporter.talkTargetId`

These are retained temporarily for backward compatibility with existing serialized assets. New quest/person content should assign `PersonDefinition` references.

## Unified Menu And Modal Model

The Tab menu is owned by `InventoryScreenController` and `InventoryScreenView`. It is currently the shared player menu shell, with pages for Inventory, Character, Spells, and Journal. Page views render data and request actions; they do not own global cursor lock or gameplay blocking.

While the shared menu is open:

- cursor is unlocked and visible;
- gameplay input is blocked through `PlayerInputReader`;
- movement, look, attack, spellcasting, and world interaction are prevented;
- inventory UI actions remain available.

Dialogue is a separate modal controlled by `DialogueScreenController`. It also blocks gameplay through `PlayerInputReader` and marks `PrototypeGameplayModalState` so interactions and enemy behavior pause while dialogue is active.

In editor and development builds, the same Tab menu can expose a `Test Lab` page. The Test Lab is development tooling, not gameplay UI. It uses the existing Tab menu input blocking and cursor ownership, calls runtime systems through `PrototypeTestLabService`, and provides centralized prototype actions for inventory, equipment, vitals, statuses, combat, quests, contracts, persistence, scenarios, and diagnostics. See `Documentation/PrototypeSystemsTestLab.md`.

Maintenance 4.4B adds a prototype scene/menu usability layout standard for PrototypeScene: labeled zones, repeated pickup groups, clearer contract/quest/combat/status areas, Test Lab teleport points, and menu layout guidance. See `Documentation/PrototypeSceneAndMenuUsability.md`.

The reset controller closes dialogue and the shared menu explicitly before restoring gameplay input.

## Quests Versus Contracts

Quests and contracts are intentionally separate high-level systems.

Quests:

- represent narrative or world-progression tasks;
- can have stages;
- can use prerequisites;
- live in `PlayerQuestLog`;
- use `QuestInstance` runtime state;
- display giver information through person metadata when available.

Contracts:

- represent formal task/reward jobs;
- do not currently use staged narrative flow;
- live in `PlayerContractJournal`;
- use `ContractInstance` runtime state.

Shared pieces:

- `ContractObjectiveDefinition` and `ContractObjectiveInstance` are reused by both systems.
- `ContractRewardDefinition` is reused by both systems.
- Defeat, collect, delivery, talk, and reach-location objectives are composed into higher-level systems.

One system should not claim rewards or complete runtime state owned by the other.

## Rewards

Quest and contract rewards currently grant item rewards only. Both systems:

- require the runtime entry to be completed before claiming;
- check inventory capacity before granting;
- leave reward state unclaimed if inventory cannot accept the full reward;
- transition to `RewardClaimed` after successful grant;
- prevent duplicate claims by requiring the state to be exactly `Completed`.

No currency, reputation, experience, or mixed reward transaction system exists yet.

## Prototype Reset Contract

Normal prototype reset is explicit and does not reload the scene.

Reset currently:

- ends active dialogue;
- closes the shared menu;
- returns the player to the configured spawn transform;
- restores health, stamina, and mana to maximum;
- clears temporary timed status effects before restoring vitals;
- clears melee and spell cooldowns;
- clears defeated input blocking and queued gameplay actions;
- returns the enemy to its start transform;
- resets enemy attack cooldown and pursuit state;
- resets enemy defeat reporting;
- resets enemy loot-drop state for future defeats;
- restores enemy health to maximum;
- preserves inventory contents;
- preserves equipment;
- preserves active/completed quest state;
- preserves active/completed contract state;
- preserves reward-claim state;
- preserves person identity and registry state.

Development-only quest reset helpers remain separate from normal reset.

## Prototype Scene Contents

The prototype scene intentionally contains:

- `Prototype Player`
- `Prototype Player Spawn`
- `Prototype Ground`
- `Directional Light`
- `Global Volume`
- `Prototype Enemy`
- `Prototype Dialogue NPC`
- `Prototype Quest Investigation Area` as an invisible trigger
- potion, sword, and helmet pickups
- inventory/dialogue/HUD/enemy health/interaction prompt canvases
- `Dialogue Controller`
- `EventSystem`

Removed visual clutter should not be reintroduced unless it is needed for a specific system test.

## Step 4 Extension Points

The current architecture is ready for these next directions without merging static definitions and runtime owners:

- save/load snapshots for inventory, equipment, vitals, quests, contracts, spell loadout, and person references;
- richer item categories and loot tables;
- proper quest markers and navigation;
- NPC factions, shops, schedules, and reputation as optional capabilities on top of `PersonIdentity`;
- character progression and stat growth;
- improved combat abilities and enemy variety;
- controller/VR interaction adapters using the existing interaction abstractions;
- more complete modal management if multiple UI layers become active at once.

## Known Prototype Limitations

- There is no save/load system yet.
- The quest location trigger is invisible.
- Contracts still use simple requester string data; person-linked contracts are a future adapter.
- Some legacy string identity fields remain for safe serialized fallback.
- No automated Unity play-mode tests exist yet; manual regression is the current verification path.
- Debug/HUD messages are intentionally prototype-level and may be noisy during repeated testing.
