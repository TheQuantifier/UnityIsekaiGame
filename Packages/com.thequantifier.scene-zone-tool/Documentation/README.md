# Scene Zone Tool 1.0

Scene Zone Tool is a pipeline-independent Unity editor extension for authoring named circle and polygon zones directly in the Scene view. It stores only world X/Z coordinates, so it works in empty scenes and does not require terrain, colliders, render pipelines, or a game framework.

## Requirements

- Unity 6.5 is fully tested for version 1.0.
- The tool is render-pipeline independent and does not reference Built-in, URP, or HDRP APIs.
- The authoring overlay is editor-only. Runtime querying works in players through `SceneZoneTool.Runtime`.

Unity 2022.3 LTS support is planned but is not claimed until the complete test suite has been run in that editor version.

## Install

Import the complete `SceneZoneTool` folder into the target project's `Assets` folder. Keep `Editor`, `Runtime`, `Samples`, and `Documentation` together. The development tests are intentionally distributed outside this folder and are not required by users.

To uninstall, delete `Assets/SceneZoneTool`. The separately created zone data under `Assets/SceneZones` is user content and is intentionally left in place.

## Five-minute quick start

1. Open or create a Unity scene. Scene geometry is optional.
2. Choose **Tools > Scene Zone Tools > Open Authoring Overlay**.
3. On first use, choose **Create New** and save `zones.asset`, or choose **Use Existing**.
4. Click the yellow circle or blue polygon tool.
5. Draw in the Scene view and close the shape.
6. Select the created zone from the Zone dropdown to rename, move, or redraw it.
7. Run **Tools > Scene Zone Tools > Validate Project** before committing authored data.

The selected data asset is remembered per scene by GUID in `ProjectSettings/SceneZoneToolSettings.asset`. Project-wide authoring defaults are available under **Project Settings > Scene Zone Tool**.

## Toolbar

| Tool | Behavior |
| --- | --- |
| Select Zone | Click inside a saved zone to select it. |
| Circle Zone | Click the center, then click again to set the radius. |
| Polygon Zone | Click points, drag to place spaced points, or press Enter/double-click/click the first point to close. |
| Move Points | Drag a saved point. Shift-click a point to insert a point after it. |
| Name Zone | Edit the currently selected zone name. |
| Replace Outline | Replace a circle or polygon's geometry without replacing its ID or object reference. |

Additional controls:

- Right-click or Backspace removes the last draft point.
- Shift-drag moves a draft polygon point or pending circle center.
- Escape cancels the current draft.
- Hold Alt or use middle-mouse navigation normally; the zone tool temporarily yields Scene view input.
- Press Q, W, E, or R to use Unity's View, Move, Rotate, or Scale tools. Click any zone action button to resume zone editing.
- Starting another circle or polygon discards an unfinished draft.

Point handles keep a constant screen size while zooming. The currently selected saved zone keeps a gold outline and gold point handles after the pointer moves away; the brighter hover highlight remains temporary. Hovering a reusable vertex brightens it; clicking then stores the exact existing coordinate. Zone interiors use distinct saved colors and names are displayed at calculated interior centers.

Expand **Visible Layers & Zones** above Options to show or hide zones bound to the active scene, grouped under entries marked **(Layer)**. By default, every zone in the current layer is visible and other layers are hidden. **Show All**, **Hide All**, and **Show Active Layer** provide quick visibility presets; changing a layer updates its children. Long zone lists scroll instead of stretching the overlay beyond the Scene view. The active zone is locked visible so editing feedback cannot disappear. These choices affect only the current authoring session and never modify saved zone data.

## Layers, zones, and overlap

A `SceneZoneLayerAsset` is one `.asset` file containing its `SceneZoneAsset` objects as Unity sub-assets. Zones on a non-overlapping layer may touch or share complete edges but cannot occupy positive area. Put intentionally nested geography on separate layers.

The layer stores a stable scene GUID, path, and display name. Moving a layer asset does not break the preferred-layer setting because the setting also uses its Unity GUID. Missing mappings can be removed with **Repair Scene Bindings** in Project Settings.

Coordinates and radii are rounded to three decimal places on authoring. Validation repairs missing or duplicate layer/zone identity fields deterministically, then rejects non-finite coordinates, duplicate consecutive points, zero-area polygons, self-intersections, invalid centers, and disallowed same-layer overlap. Error messages report rounded X/Z segment endpoints and the Scene view highlights relevant segments in red.

Deleting a zone uses Unity Undo. Deleting a complete layer moves its asset to the operating system recycle bin after a best-effort serialized-reference check.

## Runtime API

```csharp
using SceneZoneTool;
using UnityEngine;

public sealed class PlayerZoneTracker : MonoBehaviour
{
    [SerializeField] private SceneZoneLayerAsset layer;

    private void Update()
    {
        SceneZoneAsset current = layer.GetZoneAt(transform.position);
        if (current != null)
            Debug.Log($"Inside {current.DisplayName} ({current.ZoneId})");
    }
}
```

Useful members:

- `layer.Zones` returns all saved zones.
- `layer.TryGetZone(id, out zone)` and `layer.GetZone(id)` use stable IDs.
- `layer.GetZoneAt(position)` returns the first containing zone.
- `layer.GetZonesAt(position)` returns every containing zone, useful at shared edges or on overlapping layers.
- `zone.Contains(position)` ignores the supplied Y coordinate.
- `zone.PolygonPoints2D`, `zone.CircleCenter2D`, and `zone.CenterPoint2D` expose stored X/Z data.
- `zone.ApproximateHorizontalArea()` returns area in square world units.
- `SceneZoneLineRenderer` optionally displays one zone during gameplay.
- `SceneZoneMaterialFactory.CreateUnlitMaterial(color)` creates a transient material for the active Built-In, URP, or HDRP pipeline.

See `Samples/BasicUsage` for a reusable query component.

## Data and version control

- Zone geometry is serialized in normal Unity YAML when **Force Text** serialization is enabled.
- Zones are sub-assets of their layer file, so commit the entire layer `.asset` and its `.meta` file.
- `SceneZoneToolInfo.Version` reports the installed tool version.
- `SceneZoneAsset.DataVersion` reports the serialized schema version.
- Renamed pre-release fields migrate through Unity's `FormerlySerializedAs` support; the public 1.0 API consistently uses **Zone** terminology.

## Troubleshooting

- **The overlay is missing:** choose **Tools > Scene Zone Tools > Open Authoring Overlay** or enable it from the Scene view Overlays menu.
- **The overlay was dragged off-screen:** choose **Tools > Scene Zone Tools > Reset Authoring Overlay Position**.
- **Drawing is disabled:** the selected layer is bound to a different scene. Open that scene or explicitly rebind the layer in Options.
- **A zone cannot close:** inspect the error box and red segments for self-intersection or same-layer overlap.
- **A layer lost its zones:** select the layer asset and use **Repair Embedded Zone List** in its Inspector.
- **A moved layer no longer loads:** use **Project Settings > Scene Zone Tool > Repair Scene Bindings**, then select the layer again.
- **The zone tool is capturing the pointer:** press Q, W, E, or R to return control to the corresponding Scene view tool. Click a zone action button when you want to resume authoring.
- **Runtime outlines have no visible material:** confirm that the active render pipeline supplies its standard unlit shader, or assign a compatible material override to `SceneZoneLineRenderer`.

## Known limitations

- Zones are two-dimensional and ignore elevation.
- One preferred layer is remembered per scene, though any number of layer assets may exist and can be selected.
- Runtime authoring, geospatial projections, and procedural generation are outside the 1.0 scope.
- Windows editor behavior is tested locally. Other editor operating systems use only Unity APIs, but should be verified on native hardware before making platform-specific guarantees.

## Support information

When reporting an issue, include the Scene Zone Tool version, Unity version, operating system, render pipeline, reproduction steps, and the affected layer asset when it can be shared. Run **Validate Project** and include relevant Console messages.
