# Game menus

Open `Assets/Scenes/Mon/SceneGame/Start.unity` to edit or press Play.
The Canvas, panels, buttons, text, and EventSystem are saved in each scene and can be edited in the Hierarchy and Inspector.
Start opens LevelSelect. Settings opens a panel and Back returns to the page that opened it.
Exit quits a built player; inside the Unity Editor it stops Play mode.

The five existing scenes in `Assets/Scenes/TestLevel` are used directly.
Start is the first scene in Build Settings. Existing test scenes are retained after the campaign.
If a Build Profile overrides the global scene list, include Start, LevelSelect, and Level1 through Level5 there as well.

TurnGameManager's confirmed victory calls GameSceneManager.CompleteActiveLevel before GameEnded.
Defeat and Restart do not unlock anything. Replaying an earlier level never removes progress.
LevelResultNavigation adds navigation buttons below the existing result UI in campaign levels.

PlayerPrefs saves `Progress.UnlockedLevel` (defaults to 1), `Progress.Level1.Complete`
through `Progress.Level5.Complete`, `Settings.Sound`, and `Settings.Fullscreen`.
Sound uses the global AudioListener volume. Fullscreen is applied to the built game window.
These settings are also applied when starting Play directly in a level.

Edit UI objects below Canvas to change labels, spacing, anchors, images, and button appearance.
MenuScreen.cs only controls navigation, settings, and level lock state.
Use `Tools > Project D > Rebuild Editable Menu Scenes` only when you intentionally want to replace both menu layouts with the defaults.
GameSceneManager is inside ProjectD.Menus to avoid a conflict with UnityEngine.SceneManagement.SceneManager.
