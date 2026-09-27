# Basic Usage Sample

Open `BasicZoneDemo.unity` to inspect a polygon, a circle, runtime outline renderers, and a movable query object. `BasicZones.asset` contains the two zones as embedded sub-assets. At runtime, the sample selects the active pipeline's standard unlit shader and creates transient materials, so it works in Built-In, URP, and HDRP projects without shipping pipeline-specific material assets.

`SceneZoneQueryExample` demonstrates the smallest normal runtime integration:

1. Add the component to a moving GameObject.
2. Assign a `SceneZoneLayerAsset`.
3. Move the GameObject across authored zones in the Scene view. The sample previews queries in Edit Mode by default.
4. Enter Play Mode, focus the Game view, and use WASD or the arrow keys to move the visible yellow marker through the zones.

The component calls `GetZoneAt(transform.position)` and logs its initial result and subsequent containing-zone changes. Disable **Preview In Edit Mode** if you only want runtime queries, or disable **Allow Keyboard Movement** when adapting the example to your own movement system. The sample's keyboard handling uses IMGUI events, so it does not require either Unity input package. You can also run **Query Current Position** from the component's context menu. It does not alter the zone asset and does not depend on terrain, physics, or a render pipeline.
