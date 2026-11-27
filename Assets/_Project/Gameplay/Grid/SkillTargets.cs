using System.Collections.Generic;
using UnityEngine;

namespace ProjectD.Gameplay.Turns
{
    internal static class SkillTargets
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        internal static IEnumerable<Tile> For(Character actor)
        {
            if (actor == null || !actor.CanUseSkill || GridManager.Instance == null) yield break;
            Tile origin = GridManager.Instance.GetTile(actor.CurrentLocation);
            if (actor is Rogue rogue)
                foreach (var tile in RogueRange(origin, rogue.LockpickRange)) yield return tile;
            else if (actor is SwordMan || actor is ArcherMan)
                foreach (var tile in Lanes(origin, actor is ArcherMan)) yield return tile;
        }

        internal static IEnumerable<Tile> RogueRange(Tile origin, int distance)
        {
            if (origin == null) yield break;
            foreach (Tile tile in Object.FindObjectsByType<Tile>())
            {
                if (tile.gameObject.scene != origin.gameObject.scene) continue;
                Vector2Int delta = tile.gridPosition - origin.gridPosition;
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= distance) yield return tile;
            }
        }

        internal static IEnumerable<Tile> Lanes(Tile origin, bool arrow)
        {
            var grid = GridManager.Instance;
            if (origin == null || grid == null) yield break;
            foreach (var direction in Directions)
            {
                var from = origin;
                while (true)
                {
                    var to = grid.GetTile(from.gridPosition + direction);
                    if (to == null || grid.IsBlockedByWall(from, to, null, arrow)) break;
                    yield return to;
                    if (!arrow) break;
                    from = to;
                }
            }
        }

        internal static bool Contains(PlayerMove player, Tile target)
        {
            if (player == null || target == null || !player.TryGetComponent(out Character actor)) return false;
            foreach (Tile tile in For(actor))
                if (tile == target) return true;
            return false;
        }
    }
}
