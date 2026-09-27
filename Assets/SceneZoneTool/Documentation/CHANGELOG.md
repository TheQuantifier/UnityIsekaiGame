# Changelog

## 1.0.3

- Deferred layer and zone file dialogs until the current Scene view overlay layout pass finishes.
- Fixed the `EndLayoutGroup: BeginLayoutGroup must be called first` error when creating or deleting layers and zones from the authoring overlay.
- Completed circles now leave authoring mode, preventing the next Scene-view click from replacing the circle that was just created.
- Restricted circle geometry changes to new-zone drafts and the explicit Replace Selected Outline action.
- Project validation now detects and removes abandoned embedded circle and polygon drafts while preserving valid zones.
- Unusable legacy zone records are no longer rendered as unattached Scene-view points.
- The authoring controller reports each persisted incomplete zone by name and directs users to the Validate Project cleanup command without repeatedly spamming the Console.
- New zones are now registered as atomic Unity Undo operations, so undo removes both the layer reference and embedded sub-asset while redo restores both.
- Undo and redo now refresh authoring state immediately instead of leaving stale selected-zone references or unfinished geometry state.

## 1.0.2

- Released Scene view input whenever View, Move, Rotate, Scale, Alt-navigation, or middle-mouse navigation is active, preventing the authoring overlay from trapping the pointer.
- Restored zone authoring explicitly when an action button is selected.
- Added regression coverage and troubleshooting guidance for switching between zone authoring and normal Scene view controls.

## 1.0.1

- Added automatic Built-In, URP, and HDRP unlit-material selection for runtime zone outlines and the Basic Usage marker.
- Removed sample material assets that produced Asset Store SRP compatibility warnings.
- Replaced the global zone-change event with per-zone subscriptions for clean Fast Enter Play Mode behavior.
- Converted editor-only visual caches from static state to controller-owned state.
- Normalized C# source files to LF line endings and enforced the convention through `.gitattributes`.

## 1.0.0

- Initial standalone release candidate.
- Polygon and circle zone authoring in the Unity Scene view.
- Layer assets with embedded, named zone sub-assets.
- Point movement, insertion, deletion, redraw, and shared-coordinate reuse.
- Same-layer overlap validation and visual diagnostics.
- Scene-to-layer preference persistence through stable asset GUIDs.
- Runtime zone queries and optional line rendering.
- Canonical Zone terminology with serialized migration from pre-release fields.
- Project Settings, project validation, help, documentation, and version metadata.
- Expanded geometry, workflow, persistence, and runtime test coverage.
- Automatic discovery of a single zone layer already bound to the active scene.
- Persistent gold outline and point highlighting for the currently selected zone.
- Edit Mode query feedback in the Basic Usage sample, with an optional runtime-only toggle.
- Playable Basic Usage sample with a top-down camera, visible query marker, on-screen status, and package-independent keyboard movement.
- Circle and polygon outline replacement while preserving stable zone identity and references.
- Enter-to-rename, multi-layer/zone visibility controls, smooth clipped overlap previews, and self-repairing legacy identities during validation.
- Expanded About window with documentation, settings, validation, feature, and environment links.
- Current-layer-only default visibility, clearer Layers & Zones labeling, and an off-screen overlay recovery command.
- Added a Show Active Layer preset and bounded scrolling for long visibility lists.
