using ProjectD.Menus;
using UnityEngine;

/// <summary>Scene navigation for a confirmed result from this level's turn manager.</summary>
[DisallowMultipleComponent]
public class GameResultController : MonoBehaviour
{
    [SerializeField] private TurnGameManager turnManager;

    public TurnGameManager TurnManager
    {
        get
        {
            if (turnManager != null) return turnManager;
            foreach (var manager in FindObjectsByType<TurnGameManager>())
                if (manager.gameObject.scene == gameObject.scene)
                {
                    turnManager = manager;
                    break;
                }
            return turnManager;
        }
    }

    public int Level
    {
        get
        {
            for (int level = 1; level <= GameSceneManager.LevelCount; level++)
                if (gameObject.scene.path == GameSceneManager.LevelPath(level)) return level;
            return 0;
        }
    }

    public bool CanOpenNextLevel => TurnManager != null && TurnManager.IsGameComplete &&
        TurnManager.PlayerWon && Level >= 1 && Level < GameSceneManager.LevelCount &&
        GameSceneManager.IsUnlocked(Level + 1) &&
        Application.CanStreamedLevelBeLoaded(GameSceneManager.LevelPath(Level + 1));

    public void OpenNextLevel()
    {
        if (CanOpenNextLevel) GameSceneManager.OpenLevel(Level + 1);
    }

    public void RetryLevel()
    {
        if (TurnManager == null || !TurnManager.IsGameComplete) return;
        GameSceneManager.ReloadCurrentScene();
    }

    public void OpenLevelSelect() => GameSceneManager.OpenLevelSelect();

    public void ReturnToGuild()
    {
        if (TurnManager != null && TurnManager.IsGameComplete && TurnManager.PlayerWon)
            GameSceneManager.OpenLevelSelect();
    }
}
