using UnityEngine;
// Existing scenes use Player as the third character. Explicit components take priority.
public static class CharacterSetup
{
    public static Character Ensure(PlayerMove player)
    {
        Character character = player.GetComponent<Character>();
        if (character != null) return character;
        switch (player.name.ToLowerInvariant())
        {
            case "player1": return player.gameObject.AddComponent<Rogue>();
            case "player2": return player.gameObject.AddComponent<ArcherMan>();
            case "player3":
            case "player": return player.gameObject.AddComponent<SwordMan>();
            default: return player.gameObject.AddComponent<Character>();
        }
    }
}

