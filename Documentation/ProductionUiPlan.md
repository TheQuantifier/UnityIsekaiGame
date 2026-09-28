# Production UI Plan

## Product boundary

Phase 4 develops the real player interface. `Assets/_Project/Scenes/Prototype` remains a development integration scene until the first production world scene is authored, and `Assets/_Project/Prototype` remains a holding area for temporary world art and prefabs. New reusable UI code belongs under `Assets/_Project/Runtime/UI`; production presentation assets belong under `Assets/_Project/Presentation`; configuration belongs under `Assets/_Project/Configuration`.

Prototype-only world assets are not promoted merely by moving them. They move into production folders during Phase 5 only after their visuals, licensing, performance, naming, and reuse expectations are accepted.

## Visual direction

- Medieval tavern palette built from dark timber brown, leather brown, parchment, brass, and muted gold.
- Rounded panels, buttons, inputs, toggles, and scroll handles form the default silhouette.
- Gold is reserved for titles, focus, selection, and important actions rather than used as a general background.
- Destructive actions use a restrained oxblood red; positive actions use muted olive so both remain compatible with the warm palette.
- Text maintains strong contrast against every brown surface and does not rely on color alone to communicate state.

## Implementation groups

1. Production theme, canvas scaling, common controls, input ownership, and UI audit.
2. HUD resources, interaction prompts, targets, hotbar, quest tracking, and notifications.
3. Main menu shell, navigation persistence, inventory, equipment, and item details.
4. Character, skills, spells, journal, quests, party, and map foundation.
5. Dialogue, guild, trade, crafting, loot, containers, and travel interfaces.
6. Accessibility, responsive-layout validation, controller navigation, performance, and final polish.

## Engineering rules

- Screens consume real gameplay services and definitions; mock data is limited to isolated tests.
- Hidden screens do not rebuild or poll.
- Repeated rows and slots are reused where practical.
- Gameplay input is suppressed while a modal interface owns focus.
- Mouse capture is controlled by the central menu/input policy rather than individual windows.
- Every production screen must be usable at the supported minimum resolution without clipping.
- New screens include state and navigation tests before their implementation group is closed.

## Phase 4 first-group completion criteria

- No production UI class is named as a prototype theme.
- All screen-space canvases use resolution-independent scaling.
- Buttons and common controls share the brown-and-gold rounded visual language.
- Existing inventory, character, spell, journal, party, save/load, dialogue, guild, market, crafting, travel, and prompt interfaces compile against the production theme.
- Theme behavior has focused EditMode coverage and the complete EditMode and PlayMode suites remain green.

The concrete surface inventory, ownership decisions, and migration targets are maintained in `ProductionUiAudit.md`.
