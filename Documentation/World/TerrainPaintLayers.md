# Terrain Paint Layers

The current prototype terrain painting utility writes directly into Unity `TerrainData` alphamaps. Do not use that destructive workflow for the production world. The replacement must preserve an immutable source terrain and store every authored paint pass as a separate, source-controlled layer.

## Required model

- A terrain paint stack asset references the immutable base terrain and an ordered list of named paint layers.
- Each layer stores only its own splat-weight mask and settings, including enabled state, opacity, blend mode, and target terrain textures.
- Painting and erasing edit the selected layer, never the source terrain or another layer.
- The editor can hide, reorder, duplicate, clear, restore, and delete individual layers non-destructively.
- A deterministic compose command rebuilds generated client `TerrainData` alphamaps from the base plus enabled layers.
- Generated alphamaps are disposable build artifacts; the layer stack and masks are the authored source of truth.
- Composition validates texture references, dimensions, weight normalization, and terrain-tile seams before applying changes.
- Server terrain extraction continues to copy only height/collision data. Paint layers, terrain textures, and composed alphamaps remain client presentation data.

The existing `PrototypeTerrainPaintTool` should be migrated to this model before the terrain is repainted. This prevents another scene extraction, failed experiment, or accidental erase from destroying authored texture work.

## Group 3 implementation audit

The production client currently has four terrain tiles and a four-texture palette (grass, dirt, packed dirt, and rock). `PrototypeTerrainPaintTool.PaintTerrain` is the only project-owned code that calls `TerrainData.SetAlphamaps`; it generates a complete height/slope map and overwrites each tile in place. Although layer assignment records Unity Undo, the generated alphamap write is not an independently editable or durable authoring operation.

The dedicated-server extractor already has the correct boundary. It copies heightfields, tree collision data, and collision meshes into the shared content package, then deliberately removes `TerrainLayer` references. The new paint system therefore remains entirely client/editor owned and does not alter the server protocol or authoritative simulation.

## Asset ownership

- `TerrainPaintStackAsset` is the authored entry point for one contiguous terrain set. It owns an ordered list of paint-layer assets and a tile manifest.
- Every tile manifest entry references an immutable base `TerrainData` and a disposable generated `TerrainData`. The scene references only the generated copy.
- `TerrainPaintLayerAsset` stores its enabled state, opacity, blend mode, target texture, and one binary weight mask per tile. Binary Unity assets avoid line-ending corruption and keep masks independently source controlled.
- The immutable base captures the terrain appearance that existed at migration time. It is never a paint target. Recomposition starts from that base every time, making the same inputs produce the same alphamaps.
- Generated terrain assets live under a clearly named `Generated` folder and may be recreated from the stack. They are never treated as the authored source of truth.

## Implementation order

1. Add the stack, layer, tile, mask, blend-mode, validation, and deterministic composition model with EditMode coverage.
2. Add a one-time migration command that snapshots the current four tiles, creates generated copies, rewires both `Terrain` and `TerrainCollider`, and verifies that the first composition is visually lossless.
3. Replace the destructive paint command with a Scene-view layer painter supporting brush size, falloff, strength, paint, erase, and continuous-stroke Undo grouping.
4. Add layer controls for visibility, opacity, reorder, duplicate, rename, clear, restore, and delete. Every command records Undo for the exact assets it changes.
5. Add pre-compose checks for palette references, mask dimensions, normalized weights, missing tiles, generated/base aliasing, and cross-tile edge continuity. A failed validation must leave generated terrain untouched.
6. Re-run client scene, terrain usability, dedicated-server extraction, server collider, and deterministic-recompose tests before the group is merged.

## Acceptance criteria

- No authoring command writes to an immutable base terrain.
- Painting or erasing one layer cannot mutate another layer.
- Hide, reorder, clear, delete, paint, and erase all support Undo and Redo.
- Recomposition after deleting generated outputs reconstructs the same normalized alphamaps.
- Adjacent tile borders agree within the configured seam tolerance.
- Server extraction contains no paint masks, textures, or client terrain materials and retains identical height/collision coverage.
