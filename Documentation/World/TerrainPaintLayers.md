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
