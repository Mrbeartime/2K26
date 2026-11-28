using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectD.EditorTools.LevelDesign
{
    internal enum Ground { Void, Floor, Rising }
    internal enum Actor { None, Warrior, Rogue, Archer, SwordSkeleton, BowSkeleton }
    internal enum Prop { None, Goal, Lever, Plate, BowTarget, ArrowTrap, SpikeTrap, LinkedSpikes }
    internal enum Edge { None, Wall, Door, RogueDoor, BrokenWall }
    internal enum Facing { North, East, South, West }

    [Serializable]
    internal sealed class TraceCell
    {
        public int x, y;
        public Ground ground;
        public Actor actor;
        public Prop prop;
        public Facing actorFacing, propFacing;
        public int riseRound = 1, interval = 2;
        public string channel = "";
    }

    // Edges belong to the left or upper cell, so a shared edge has one identity.
    [Serializable]
    internal sealed class TraceEdge
    {
        public int x, y;
        public bool east, startOpen;
        public Edge kind;
        public string channel;
    }

    [Serializable]
    internal sealed class TraceLevelPlan
    {
        public int version = 1, width = 9, height = 9, turns = 5;
        public string reference = "Docs/LevelDesign/References/PP-07.png";
        public Rect crop = new Rect(.092f, .092f, .816f, .816f);
        public List<TraceCell> cells = new List<TraceCell>();
        public List<TraceEdge> edges = new List<TraceEdge>();

        public TraceCell Cell(int x, int y) => cells.Find(c => c.x == x && c.y == y);
        public TraceEdge Border(int x, int y, bool east) => edges.Find(e => e.x == x && e.y == y && e.east == east);
        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        public static bool Source(Prop p) => p == Prop.Lever || p == Prop.Plate || p == Prop.BowTarget;
        public static string Key(string channel) => (channel ?? "").Trim().ToUpperInvariant();

        public static bool IsWall(Edge kind) => kind == Edge.Wall || kind == Edge.BrokenWall;
        public static bool IsDoor(Edge kind) => kind == Edge.Door || kind == Edge.RogueDoor;
        public static bool RandomFacing(Prop kind) => kind == Prop.Plate || kind == Prop.SpikeTrap || kind == Prop.LinkedSpikes;
        public static string Position(int x, int y) => $"({x + 1}, {y + 1})";

        public static bool FitsEdge(int x, int y, bool east, Edge kind, int columns, int rows)
        {
            // The west and north perimeter use owner -1 in the perpendicular axis.
            int along = east ? y : x;
            int across = east ? x : y;
            int limit = east ? columns : rows;
            return along >= 0 && along < (east ? rows : columns) &&
                (IsWall(kind) ? across >= -1 && across < limit : across >= 0 && across < limit - 1);
        }

        internal static Color LinkColor(string channelName)
        {
            switch (Key(channelName))
            {
                case "BLUE": return new Color(.25f, .55f, 1);
                case "GREEN": return new Color(.3f, .85f, .4f);
                case "RED": return new Color(1, .35f, .3f);
                case "PURPLE": return new Color(.72f, .4f, 1);
                case "YELLOW": return Color.yellow;
                case "": return Color.white;
                default:
                    uint hash = 0;
                    foreach (char c in TraceLevelPlan.Key(channelName)) hash = unchecked(hash * 31 + c);
                    return Color.HSVToRGB((hash % 360) / 360f, .65f, 1);
            }
        }

        public string FormatError()
        {
            if (version != 1 || width < 1 || width > 32 || height < 1 || height > 32 || turns < 1)
                return "Use a version 1 plan with a 1–32 cell grid and a positive turn limit.";
            if (!(crop.width > 0 && crop.height > 0 && crop.x >= 0 && crop.y >= 0 &&
                  crop.xMax <= 1.00001f && crop.yMax <= 1.00001f)) return "The reference crop must fit inside the image.";
            if (cells == null || edges == null) return "Missing cell or edge list.";
            var positions = new HashSet<Vector2Int>();
            foreach (var c in cells)
                if (c == null || !Inside(c.x, c.y) || !positions.Add(new Vector2Int(c.x, c.y)) ||
                    !Enum.IsDefined(typeof(Ground), c.ground) || !Enum.IsDefined(typeof(Actor), c.actor) ||
                    !Enum.IsDefined(typeof(Prop), c.prop) || !Enum.IsDefined(typeof(Facing), c.actorFacing) ||
                    !Enum.IsDefined(typeof(Facing), c.propFacing))
                    return "Invalid or duplicate cell in the plan.";
            var borders = new HashSet<Vector3Int>();
            foreach (var e in edges)
                if (e == null || !FitsEdge(e.x, e.y, e.east, e.kind, width, height) ||
                    !borders.Add(new Vector3Int(e.x, e.y, e.east ? 1 : 0)) || !Enum.IsDefined(typeof(Edge), e.kind) || e.kind == Edge.None)
                    return "Invalid or duplicate edge in the plan.";
            return null;
        }

        public string CheckPlayable()
        {
            var error = FormatError();
            if (error != null) return error;
            bool hero = false, goal = false;
            var sources = new HashSet<string>();
            var spikeSources = new HashSet<string>();
            var doors = new HashSet<string>();
            var spikes = new HashSet<string>();
            foreach (var c in cells)
            {
                if (c.ground == Ground.Void && (c.actor != Actor.None || c.prop != Prop.None))
                    return $"Paint floor under the object at {Position(c.x, c.y)}.";
                if (c.ground == Ground.Rising && (c.riseRound < 1 || c.riseRound > turns || c.prop != Prop.None || c.actor != Actor.None))
                    return $"Rising floor at {Position(c.x, c.y)} needs an in-range round and an empty cell.";
                if ((c.prop == Prop.ArrowTrap || c.prop == Prop.SpikeTrap) && c.interval < 1)
                    return $"Trap at {Position(c.x, c.y)} needs a positive interval.";
                hero |= c.actor == Actor.Warrior || c.actor == Actor.Rogue || c.actor == Actor.Archer;
                goal |= c.prop == Prop.Goal;
                string key = Key(c.channel);
                if ((Source(c.prop) || c.prop == Prop.LinkedSpikes) && key.Length == 0)
                    return $"Give the mechanism at {Position(c.x, c.y)} a link name.";
                if (Source(c.prop)) sources.Add(key);
                if (c.prop == Prop.Lever || c.prop == Prop.Plate) spikeSources.Add(key);
                if (c.prop == Prop.LinkedSpikes) spikes.Add(key);
            }
            foreach (var e in edges)
            {
                if (IsWall(e.kind)) continue;
                var a = Cell(e.x, e.y);
                var b = Cell(e.x + (e.east ? 1 : 0), e.y + (e.east ? 0 : 1));
                if (a == null || b == null || a.ground == Ground.Void || b.ground == Ground.Void)
                    return $"The door needs floor in {Position(e.x, e.y)} and {Position(e.x + (e.east ? 1 : 0), e.y + (e.east ? 0 : 1))}. Paint the missing floor or erase the door.";
                if (e.kind == Edge.Door)
                {
                    string key = Key(e.channel);
                    if (!sources.Contains(key)) return $"Door at {Position(e.x, e.y)} needs a matching lever, plate or target.";
                    doors.Add(key);
                }
            }
            foreach (var c in cells)
            {
                string key = Key(c.channel);
                if (Source(c.prop) && !doors.Contains(key) && !(c.prop != Prop.BowTarget && spikes.Contains(key)))
                    return $"Mechanism at {Position(c.x, c.y)} has no compatible receiver for '{c.channel}'.";
                if (c.prop == Prop.LinkedSpikes && !spikeSources.Contains(key))
                    return $"Spikes at {Position(c.x, c.y)} need a matching plate or lever.";
            }
            if (!hero || !goal) return "Paint at least one playable hero and one goal.";
            return null;
        }
    }
}
