# Scene Zone Free Select Tool

This folder is standalone. Copy it into another Unity project's `Assets` folder to use it there. It has no dependency on Unity Isekai Game code or scene geometry.

## Use

1. Open any Unity scene. The scene may be completely empty; no scene geometry is required.
2. Choose **Tools > Scene Zone Tools**. The overlay is hidden by default and opens only when requested. On first use in each scene, choose **Create New** to select where `zones.asset` will be saved, or **Use Existing** to select a Scene Zone layer already stored in the project's `Assets` folder.
3. Drag the overlay to dock it or leave it as a detachable floating toolbar.
4. Select a **Layer** and **Zone**, or press **+** and save the suggested `zones.asset`. Each layer file contains all of its zones and a stable link to its Unity scene.
5. Use the yellow circle or blue polygon button, then work directly in the Scene view.

The tools are:

- **Yellow Circle**: create a circle by clicking its center and then clicking again to set its radius.
- **Blue Polygon**: click individual points or hold the left mouse button and drag. Click the green first point, double-click, or press Enter to close the zone.
- **Select Zone**: click inside an existing zone to make it active.
- **Move Points**: click and drag any saved polygon point. Shift+left-click inserts a new point immediately after it in polygon order, initially halfway toward the following point; keep dragging to place the new point elsewhere. You can also begin a normal left-drag and press the right mouse button while still holding left to insert the new point.
- **Name Zone**: rename the saved zone currently selected in the Zone dropdown.

Use **Redraw Selected Outline** to replace an existing polygon without deleting its zone asset. Its stable ID, name, layer membership, and references from gameplay assets remain intact. Canceling a redraw leaves the previously saved outline unchanged.

The minus button beside **Layer** moves the selected layer file and all of its zones to the operating system's Recycle Bin after showing assets that depend on it. The minus button beside **Zone** deletes only the selected zone through Unity's Undo system. Starting another circle or polygon automatically discards an unfinished zone.

The tool stores the chosen data-file location separately in `ProjectSettings/SceneZoneToolSettings.asset`. That settings file maps each Unity scene to its preferred layer asset by stable Unity asset GUID, so moving or renaming the file does not break the association. Opening the tool again in that scene automatically loads the remembered file, while the **+** button can create and switch the scene to a new layer at any time.

While creating a polygon or circle, right-click removes the most recently placed pending point. Shift-drag a pending polygon point, or the pending circle center, to reposition it before completing the zone. Backspace also removes the last polygon draft point, Escape cancels, and Alt remains available for Scene view navigation.

The tool has no canvas and no placement bounds. Zones can be drawn at any practical world coordinate and at any size. The optional display height only controls where editor handles are shown; it is never stored in a zone.

Polygon points are solid colored Scene handles: the first point is green, active points are blue, shared/inactive-layer points are pale blue, selected points are yellow, and center points are orange. Their handle size is screen-relative, so they remain visible at the same apparent size while zooming.

Every zone receives a persistent, randomly selected fill color that is unique within its layer. The Scene view renders that color as a translucent interior beneath the outline and displays the zone name in red at its calculated interior center.

While drawing a polygon, hovering close to a saved polygon vertex brightens that vertex and reuses its exact coordinate on click. Overlap messages identify segments with their rounded X/Z endpoint coordinates instead of temporary letter labels.

## Two-dimensional storage

Saved geometry is strictly two-dimensional:

- each polygon point is a `Vector2(worldX, worldZ)`;
- circle centers and authored center points are also `Vector2(worldX, worldZ)`;
- no height or Y coordinate is stored;
- containment, overlap, area, shared-edge, and vertex-attachment logic ignores height.

When an outline is displayed, the tool draws those saved X/Z coordinates on the chosen scene-plane display height. Height-aware zones can be added later without changing the current zone semantics.

## Layer files and shared borders

A `SceneZoneLayerAsset` is the single `.asset` file for one logical layer. Its zones are embedded Unity sub-assets. Each zone stores a stable ID, name, shape, ordered points or circle geometry, optional center, and line metadata. The layer—not each zone—stores the Unity scene GUID/path/name association.

Unfinished shapes are transactional editor drafts and are not added to the layer file until their circle or polygon is completed. Compilation, closing the overlay, or an interrupted drawing session therefore cannot leave empty zone sub-assets behind. Polygon completion also rejects duplicate edges, non-finite coordinates, zero-area outlines, and self-intersection.

The painter keeps every outline in the active layer visible and, by default, prevents zones in that layer from occupying the same area. Zones may touch and share edges. Put intentionally nested regions, such as towns and kingdoms, on different layers.

Saved polygon, circle-center, and center-point X/Z coordinates are rounded to three decimal places. Circle radii use the same precision. This keeps authored data stable and readable without pulling the mouse pointer away from its exact placement position.

When an overlap is rejected, the window reports rounded X/Z segment endpoints, such as `Draft segment [<1,1>,<2,3>] overlaps Town Boundary segment [<4,5>,<7,8>]`, and draws the involved segments as thick red lines. During circle creation, conflicting area is previewed in red before the radius click.

## Runtime access

The layer API provides direct access without parsing the asset file:

- `layer.Zones` (also exposed as `layer.Boundaries`) lists every zone;
- `layer.TryGetZone(id, out zone)` and `layer.GetZone(id)` retrieve zones by stable ID;
- `layer.FindZoneByName(name)` retrieves a zone by display name;
- `layer.GetZonePoints(id)` returns ordered polygon `Vector2` X/Z coordinates;
- `zone.Contains(worldPosition)` tests X/Z only, regardless of the supplied Y value;
- `zone.PolygonPoints2D`, `zone.CircleCenter2D`, and `zone.CenterPoint2D` expose the stored data.

To display an outline during gameplay, add `SceneZoneLineRenderer` to any GameObject and assign a zone. It draws the saved X/Z coordinates at the component's display-plane height and refreshes automatically when the referenced zone changes.

The Unity Isekai Game integration is deliberately outside this folder. It binds this generic geometry to game Places, Locations, and the logical runtime scene key, allowing political ownership or game-scene identity to change without changing the standalone geometry or its Unity editor scene association.

For this project, use **Tools > Unity Isekai Game > World > Bind Zone To Place** to create that game-specific binding. Existing bindings reference the embedded zone sub-asset directly, so moving points or redrawing an outline immediately changes runtime containment without recreating the binding. Creating a new unbound zone does not assign gameplay meaning until a binding is created and the definition catalog is rebuilt; the binding window performs that rebuild automatically.
