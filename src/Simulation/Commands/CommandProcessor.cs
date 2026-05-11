using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Commands;

/// <summary>
/// Applies a tick's commands to the world in a deterministic order.
///
/// Ordering:
///   1. By <see cref="Command.Player"/> ascending (player id is the primary discriminator).
///   2. By <see cref="Command.Sequence"/> ascending within the player (the input layer assigns
///      monotonic per-player sequence numbers so two commands from the same player on the same
///      tick still have a stable relative order).
///
/// Commands targeting non-existent or unowned units are silently dropped — clients must not
/// trust that a unit still exists between input collection and execution.
/// </summary>
public static class CommandProcessor
{
    // Spiral offsets covering up to a 7x7 region around the target tile. Used to spread out a
    // multi-unit move so units don't all stack on a single target tile. 49 slots is more than
    // enough for v1's expected group sizes; falls back to "queue them on the closest available
    // tile" if a larger group is somehow assembled.
    private static readonly (int dx, int dy)[] SpiralOffsets =
    {
        (0,0),
        (1,0),(0,1),(-1,0),(0,-1),
        (1,1),(-1,1),(-1,-1),(1,-1),
        (2,0),(0,2),(-2,0),(0,-2),
        (2,1),(1,2),(-1,2),(-2,1),(-2,-1),(-1,-2),(1,-2),(2,-1),
        (2,2),(-2,2),(-2,-2),(2,-2),
        (3,0),(0,3),(-3,0),(0,-3),
        (3,1),(3,-1),(1,3),(-1,3),(-3,1),(-3,-1),(1,-3),(-1,-3),
        (3,2),(3,-2),(2,3),(-2,3),(-3,2),(-3,-2),(2,-3),(-2,-3),
        (3,3),(-3,3),(-3,-3),(3,-3),
    };

    public static void Apply(World world, IReadOnlyList<Command> commands)
    {
        if (commands.Count == 0) return;

        // Sort in-place into a fresh list. We don't mutate the caller's list.
        var ordered = new List<Command>(commands);
        ordered.Sort(Compare);

        foreach (var command in ordered)
        {
            switch (command)
            {
                case MoveCommand mc: ApplyMoveCommand(world, mc); break;
                // Attack/Gather/Build/Train arrive in M3+.
            }
        }
    }

    private static int Compare(Command a, Command b)
    {
        int cmp = a.Player.Value.CompareTo(b.Player.Value);
        if (cmp != 0) return cmp;
        return a.Sequence.CompareTo(b.Sequence);
    }

    private static void ApplyMoveCommand(World world, MoveCommand mc)
    {
        int targetTileIdx = TileIdxOf(mc.Target);
        var assigned = new HashSet<int>();

        // For each unit in EntityId order, find the next unassigned, passable tile in the spiral.
        var orderedUnits = new List<EntityId>(mc.Units);
        orderedUnits.Sort();

        int tx = targetTileIdx % Grid.Width;
        int ty = targetTileIdx / Grid.Width;

        foreach (var unitId in orderedUnits)
        {
            if (!world.TryGetEntity(unitId, out var entity) || entity is not Unit unit) continue;
            if (unit.Owner != mc.Player) continue;

            int chosen = -1;
            for (int s = 0; s < SpiralOffsets.Length; s++)
            {
                var (dx, dy) = SpiralOffsets[s];
                int nx = tx + dx;
                int ny = ty + dy;
                if ((uint)nx >= Grid.Width || (uint)ny >= Grid.Height) continue;
                if (!world.Map.IsPassable(nx, ny)) continue;
                int idx = ny * Grid.Width + nx;
                if (!assigned.Add(idx)) continue;
                chosen = idx;
                break;
            }

            if (chosen < 0)
            {
                // Fallback: just send it to the original target even if multi-unit overlap is
                // unavoidable. MovementSystem will sort out the collision.
                chosen = targetTileIdx;
            }

            unit.PendingDestinationIdx = chosen;
            unit.ClearPath();
            unit.WaitTicks = 0;
            unit.RepathsInWindow = 0;
            unit.RepathWindowStartTick = world.CurrentTick;
            unit.State = UnitState.Moving;
        }
    }

    private static int TileIdxOf(FixedVector2 position)
    {
        int x = position.X.ToInt();
        int y = position.Y.ToInt();
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x >= Grid.Width) x = Grid.Width - 1;
        if (y >= Grid.Height) y = Grid.Height - 1;
        return y * Grid.Width + x;
    }
}
