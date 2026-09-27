# Upgrading Scene Zone Tool

Back up or commit zone layer assets before upgrading. Import the new `SceneZoneTool` folder over the existing folder, allow Unity to recompile, and run **Tools > Scene Zone Tools > Validate Project**.

Version 1.0 standardized the public API on `ZoneId`, `Layer`, and `Zone`. Unity automatically migrates pre-release serialized fields named `boundaryId` and `boundaryLayer`; no duplicate legacy runtime system is retained.

Do not delete user-authored `Assets/SceneZones` data when replacing the tool itself.
