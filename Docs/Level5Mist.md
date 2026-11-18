# Level 5 Mist

Open `Assets/Scenes/MainScene/Level/Level5_Mist.unity` and press Play.
This is a separate copy of Level 5, including its current `NewUISet` HUD,
characters, traps, lighting, and entrance sequence. The campaign's Level 5
continues to point to the original scene.

## Adjusting the mist

Select `Assets/Materials/Level5Mist.mat`:

- **Mist Color**: tint of the mist.
- **Density**: opacity of both layers; start around 0.3–0.5.
- **Cloud Size (World Units)**: larger values create broader clouds.
- **Wind Speed**: drift and deformation speed; 0 stops animation.

The `Drifting Mist` object contains only two horizontal quads. Move or scale
them to change coverage, or disable their parent to compare with the base level.
Keep the sheets below the walkable surface so characters and tile highlights
stay clear. The camera background is almost black to match the reference.

Animation runs entirely in `ProjectD/Drifting Mist`: shared wind and slowly
changing procedural noise create continuous motion without textures, particles,
or a per-frame C# controller. Animation follows game time and freezes when paused.
Depth fading softens intersections, including with the orthographic camera.
The scene's Main Camera explicitly requests a depth texture for both PC and
Mobile quality settings.

This is layered mist, not a volumetric simulation. It is intended for the
existing isometric view. It does not add the reference image's tall black
foundation columns.

`Tools > Atmosphere > Create Level 5 Mist Scene` creates the scene if missing,
or opens the existing scene without overwriting edits. The scene is included
in Build Settings so Retry can reload it; it is not added to the campaign catalog.
