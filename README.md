# Unity Isekai Game

## Overview

This repository contains an early prototype for a long-term 3D isekai fantasy RPG and life-simulation game built in Unity.

The project is not an alpha, beta, complete game, or representative final experience. Current visuals, UI, content, and scene layout are temporary prototype assets used to validate reusable systems before building the first full region.

## Long-Term Vision

The goal is to create a persistent fantasy world that can be lived in, not only played through once. The intended experience is inspired by entering the world of an isekai fantasy series, where players can build an identity and choose how they want to live.

Combat and dungeon exploration are planned as important parts of the game, but they are not intended to be the only meaningful activities. Over time, the project may support players pursuing roles such as adventurer, merchant, shopkeeper, knight or guard, transporter or caravan operator, crafter, guild contractor, and other professions added as development expands.

Players should not necessarily be permanently locked into one role. A character might begin as an adventurer, later rent a shop, join the Merchant Guild, own property, or participate in several parts of the world at once.

## Planned World And Roles

The world is planned to expand gradually and may eventually contain:

- Towns and cities.
- Castles and political territories.
- Villages and outposts.
- Roads and trade routes.
- Guild halls.
- Markets and player-owned shops.
- Homes and rentable property.
- Wilderness regions.
- Caves, ruins, mines, towers, buildings, and other dungeon types.

The first major playable target is one compact region: a town, nearby wilderness, a few dungeons, and one functioning Adventurer's Guild. Later work can deepen that region and add more professions, guilds, settlements, economies, and political areas.

## Guilds And Contracts

Guilds are intended to function as labor markets and trusted contract exchanges rather than simple quest menus.

The Adventurer's Guild may eventually support jobs such as dungeon exploration, monster extermination, escorts, caravan protection, gang or bandit suppression, rescues, deliveries, transporting valuable items, gathering materials, investigations, defending locations, and recovering lost or stolen property.

Contracts may eventually be created by the simulated world, NPCs, governments, guilds, or other players. Player-created contracts would need supporting systems such as escrowed rewards, posting fees, objective verification, deadlines, requirements, failure conditions, guild reputation, and protections against item, currency, and contract fraud.

Other guilds, such as a Merchant Guild, should be able to use the same shared contract foundation for profession-specific work.

The current prototype now includes static faction and organization definitions for a prototype kingdom, Adventurer's Guild, Merchant Guild, town guard, and bandits. These are authored metadata only; ranks, memberships, reputation, diplomacy, laws, and economy are still future runtime systems.

## Economy And Player Businesses

Settlements are planned to have statistical economies rather than one fixed global price list.

Towns, cities, castles, and nations may eventually track production, consumption, supply, demand, stock levels, shortages and surpluses, transport costs, taxes and tariffs, trade-route safety, regional events, war, political conditions, and seasonal effects.

Different settlements should produce and consume different goods. This should create useful work for adventurers, merchants, transporters, shopkeepers, and guild contractors.

In addition to NPC shops, players may eventually be able to rent or purchase commercial space, open a shop, stock goods, set prices within technical and economic rules, sell to NPC customers, trade with other players, hire employees, and pay rent, wages, taxes, and guild fees.

NPC shops should provide predictable basic goods and economic stability. Player businesses should provide specialization, rare goods, imports, crafted items, and competition.

## PC And Future VR Direction

The project is currently being developed as a standard Windows PC game.

Future VR support is a long-term goal, but gameplay systems should not depend directly on VR hardware. Core actions such as interaction, purchasing, equipping, accepting contracts, using items, and opening doors should use shared game commands that can later be triggered by keyboard and mouse, gamepad, or VR controllers.

VR should enhance presence and physical interaction without requiring the underlying game simulation to be rewritten.

## Development Philosophy

This is a long-term personal project with no fixed release deadline.

Development is intended to stay incremental:

- Build small reusable systems.
- Validate them in a prototype scene.
- Integrate them into one compact playable town and region.
- Deepen the first region before expanding broadly.
- Add professions, guilds, economies, settlements, dungeons, and multiplayer-facing systems gradually.

The codebase should favor modular systems and expansion points rather than hard-coding features to one NPC, town, item, enemy, or interface.

## Current Prototype Status

The current focus is building reusable core systems in `Assets/_Project/Scenes/Prototype/PrototypeScene.unity` before creating the first full town.

Current foundations include:

- First-person movement and camera controls.
- Keyboard, mouse, and gamepad input through the Unity Input System.
- Reusable center-screen world interaction.
- Shared game-data definitions, stable IDs, catalog validation, and prototype registry lookup.
- Category/tag taxonomy for items, abilities, statuses, beings, places, factions, and damage.
- Item definitions, item rarity, world pickups, and item taxonomy metadata.
- Inventory data, stacking, stateful item instances, inventory/equipment save-data DTOs, and inventory UI.
- Save-file envelope, persistence service, save-slot metadata, atomic local writes, backups, development save/load commands, player-scoped inventory/equipment persistence, player-scoped vital/status persistence, player-scoped quest/contract persistence, and same-scene player scene/place/position persistence.
- Persistent world-entity identity handles for authored scene objects and runtime spawned world objects.
- Consumable item use.
- Health, stamina, and mana.
- Equipment, generic runtime actor statistics, and typed damage resistance modifiers.
- Static being definitions and reusable actor profiles for player/enemy base stats.
- Persistent player identity, origin, birth gifts, roles, social statuses, titles, wallet, Base Attributes, Calculated Stats, Current Resources, learned Skill/Proficiency progression, Traits, capabilities, and a unified Character System coordinator.
- Static place definitions and typed reach-location references.
- Static faction and organization definitions used by quests, contracts, people, and places.
- Melee combat and shared attack/defense damage handling.
- Enemy health, runtime stats, typed damage, pursuit, attacks, defeat, statuses, and loot drops.
- Prototype player defeat and reset controls.
- Ability/effect definitions, basic spellcasting, and projectiles.
- Spell loadout and quick-action selection.
- Dialogue system foundation with prototype NPC conversation.

Step 3 closeout documentation lives in `Documentation/Step3GameDataAndWorldTaxonomyArchitecture.md`, with regression coverage in `Documentation/Step3RegressionChecklist.md`.

Step 4 persistence foundation documentation lives in `Documentation/PersistenceServiceFoundation.md`, with the planned sequence in `Documentation/Step4PersistenceRoadmap.md`.
Player inventory/equipment persistence is documented in `Documentation/PlayerInventoryAndEquipmentPersistence.md`.
Player resources and status-effect persistence are documented in `Documentation/PlayerResourcesAndStatusPersistence.md`.
Quest and contract persistence is documented in `Documentation/QuestAndContractPersistence.md`.
Player position, scene, and place persistence is documented in `Documentation/PlayerPositionSceneAndPlacePersistence.md`.
Persistent world-entity identity is documented in `Documentation/PersistentWorldEntityIdentity.md`.
Persistence integration, recovery, rollback, and failure hardening are documented in `Documentation/PersistenceIntegrationRecoveryAndFailureHardening.md`.
Final Step 4 persistence closeout documentation lives in `Documentation/Step4PersistenceArchitecture.md`, `Documentation/Step4PersistenceSchemaInventory.md`, `Documentation/Step4PersistenceOwnershipMatrix.md`, `Documentation/Step4PersistenceKnownLimitations.md`, `Documentation/Step4PersistenceRegressionChecklist.md`, and `Documentation/Step5PersistenceMigrationGuidance.md`.
The editor/development-build Prototype Systems Test Lab is documented in `Documentation/PrototypeSystemsTestLab.md`.
PrototypeScene usability, zone layout, pickup grouping, and menu layout standards are documented in `Documentation/PrototypeSceneAndMenuUsability.md`.
Project folder ownership and M1 placement rules are documented in `Documentation/ProjectStructureOrganization.md`.
Skill and Proficiency progression is documented in `Documentation/SkillsAndProgression.md`, `Documentation/SkillLearning.md`, `Documentation/SkillGradeEffects.md`, and `Documentation/Feature5_3Persistence.md`.
Base Attributes, Calculated Stats, and Current Resources are documented in `Documentation/BaseAttributes.md`, `Documentation/CalculatedStatsRefinement.md`, `Documentation/CurrentResources.md`, `Documentation/ResourceDefinitions.md`, `Documentation/ResourceTransactions.md`, `Documentation/CharacterNumericalModel.md`, `Documentation/Feature5_4aPersistenceAndMigration.md`, and `Documentation/Feature5_4bPersistenceAndMigration.md`.
Step 5 Character System closeout documentation lives in `Documentation/CharacterSystemOverview.md`, `Documentation/CharacterOwnershipAndIdentity.md`, `Documentation/CharacterInitializationAndRestore.md`, `Documentation/CharacterSnapshotsAndQueries.md`, `Documentation/CharacterMutationBoundaries.md`, `Documentation/CharacterSystemPersistence.md`, `Documentation/CharacterSystemIntegrationContract.md`, and `Documentation/Step5Completion.md`.

### Inventory Item Instance Save Foundation

The inventory still supports the existing definition-stack flow through `PlayerInventory.AddItem` for ordinary stackable content. Items marked `AlwaysInstanced` are granted through `PlayerInventory.AddItemOrInstances`, which creates one persistent `ItemInstance` per quantity. Exact instances can move between inventory and equipment without losing their instance ID or metadata.

`PlayerInventory.CreateSaveData` / `TryRestoreFromSaveData` and `PlayerEquipment.CreateSaveData` / `TryRestoreFromSaveData` provide the first save-data shapes for slot stacks and stateful item instances. Restore is validated before live state is replaced, so missing definitions, invalid quantities, bad item-instance payloads, duplicate IDs, or incompatible equipment slots fail without partially mutating the current inventory or equipment.

These systems are still prototype-quality and may be revised substantially as larger gameplay systems are added.

## Near-Term Roadmap

Near-term work should continue building the shared foundations required for the first playable region, including:

- More complete dialogue support.
- Modular contracts and objectives.
- Adventurer's Guild systems.
- Quest and contract tracking.
- Saving and loading.
- Scene and world-state management.
- NPC foundations.
- The first town.
- Nearby wilderness and dungeons.
- Initial persistence.

The first major playable goal is not the entire envisioned world. It is one compact region that demonstrates the central fantasy: arrive in a fantasy settlement, interact with residents, join the Adventurer's Guild, accept varied contracts, travel through nearby areas, fight, explore, gather, escort, deliver, earn rewards, collect equipment and magic, and return to a town that begins to feel persistent and lived in.

## Technology

- Unity 6.5.
- C#.
- Universal Render Pipeline.
- Unity Input System.
- Unity UI for current prototype interfaces.
- Visual Studio Code for C# editing.
- Git and GitHub for source control.

The current package manifest includes Unity Input System, Universal Render Pipeline, Unity UI, AI Navigation, Timeline, the Unity Test Framework, and standard Unity modules. Package choices may change as the project matures.

## Repository Status

This repository is a development workspace for an early prototype. It should not be treated as a finished game, production-ready framework, final art direction, final UI direction, or fixed design promise.

Planned systems described in this README are goals, not guarantees that those systems currently exist. Implemented systems are being built and tested incrementally in the prototype scene before the project expands into a full playable region.
