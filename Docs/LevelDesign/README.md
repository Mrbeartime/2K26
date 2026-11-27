# Trace a design page into a Unity scene

Open **Tools → Level Design → Trace and Paint** in Edit Mode. Select **Page 7 example**, then **Generate draft scene…** and choose a new scene name under `Assets/`. Double-click that scene in the Project window and press **Play**. Select the warrior and use the turn panel to move, use a skill, interact or end a turn. The sample uses the five-turn limit printed on page 7.

## Trace another board

1. Select **New** and **Choose page image…**. The images in `Docs/LevelDesign/References` correspond to pages 7–15 of `PP.pdf`. For another document, export a design page as PNG or JPEG. Keep shared reference images inside the project directory so collaborators can open the same plan.
2. Count the columns and rows on the paper board. Set those dimensions, choose **Pick grid corners on page**, and click two opposite outer corners of the paper grid. Fine-tune the alignment with **Left**, **Top**, **Width** and **Height**. Each crop setting is a fraction of the full image. For a 1,400-pixel image, a left margin of 140 pixels is `0.1`. Use **Page opacity** to compare the trace against the paper.
3. Paint walkable cells on the **Ground** layer. Blank space is a gap. Use **Rising** and specify the appearance round for timed floors. Paint heroes and skeletons on **Actors** and goals, mechanisms and traps on **Props**. Actors and props can share a cell, including an enemy standing on a plate. Right-click to erase the selected layer. Use Unity Undo to reverse a stroke.
4. On **Edges**, click the shared border between two floor cells to place a wall, regular door or rogue door. North points to the top of the page and maps to positive world Z. For a crossbow, set **Facing** and **Every N rounds** before placing it.
5. Give a lever, pressure plate or bow target and its regular door the same **Link name**, such as `Blue`. Each source controls every compatible receiver with that name. Plates and levers can also drive **Linked Spikes**. A rogue door uses the rogue's existing skill. Repaint an item to edit its settings.
6. Select **Check plan**, then **Save plan as…** to keep an editable JSON copy. **Generate draft scene…** writes a fresh Unity scene with ordinary, editable prefab instances. Open the draft by itself for Play Mode. Regenerating a revised plan requires a fresh scene path. Scene edits and plan edits are independent.

The generator supplies the grid, turn manager, player input, test turn panel, camera and light. Draft scenes are separate from campaign progression. A victory in an unregistered draft can produce the existing progress-save warning. The checks cover placement and wiring. Play through the puzzle to establish that it is solvable within the turn budget.

The palette uses the game's existing mechanics. A repeating trap activates on multiples of its interval. Custom odd-round phases, enemy patrol routes, scripted event floors, cutscenes and final art dressing need separate scene work.

**Tools → Level Design → Verify draft generation** builds and reopens a temporary scene to check tile uniqueness, actors, links, trap timing and overwrite protection.
