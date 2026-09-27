# Changelog

## 1.0.1

- Released Scene view input whenever View, Move, Rotate, Scale, Alt-navigation, or middle-mouse navigation is active, preventing the authoring overlay from trapping the pointer.
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
