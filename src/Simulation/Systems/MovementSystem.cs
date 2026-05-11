using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Systems;

/// <summary>
/// Per-tick movement for all Units in <see cref="UnitState.Moving"/> or <see cref="UnitState.Waiting"/>.
///
/// Contract:
/// - Iterates units in <see cref="EntityId"/> order. Any system that mutates per-unit state across
///   the world MUST iterate in this order to keep the state hash stable.
/// - Path generation is lazy: A* runs the first tick a unit has a pending destination, not at
///   command-apply time. This keeps CommandProcessor cheap and lets multi-unit moves amortize
///   pathfinding across many ticks rather than a single command spike.
/// - Re-pathing: when a unit is blocked at its next waypoint for 5 consecutive ticks, it requests
///   a new path. If it has requested 3 re-paths inside the last 100 ticks, it gives up and goes
///   Idle. The threshold prevents the pathological "two villagers shuffling forever" deadlock.
/// - Tile occupancy: a unit "occupies" the tile its center is in. Occupancy moves with the
///   unit on tile-boundary crossings; the world's occupancy table is the source of truth that
///   collision checks consult.
/// </summary>
public sealed class MovementSystem
{
    // Tick threshold after which a blocked unit asks for a new path.
    private const int WaitTicksBeforeRepath = 5;

    // Max re-paths within a rolling 100-tick window before the unit gives up and goes Idle.
    private const int MaxRepathsInWindow = 3;
    private const long RepathWindowLengthTicks = 100;

    private readonly AStar _pathfinder = new();

    public void Tick(World world)
    {
        foreach (var unit in world.UnitsOrderedById())
        {
            TickUnit(world, unit);
        }
    }

    private void TickUnit(World world, Unit unit)
    {
        // 1. If a new path request is pending, resolve it now via A*.
        if (unit.PendingDestinationIdx >= 0)
        {
            ResolvePendingPath(world, unit);
        }

        if (unit.State == UnitState.Idle || !unit.HasPath)
        {
            unit.State = UnitState.Idle;
            return;
        }

        // 2. Look up the next waypoint.
        int waypointIdx = unit.Path[unit.PathIndex];
        var waypointPos = TileCenter(waypointIdx);

        // 3. Collision check: if the waypoint tile is different from the unit's current tile
        //    AND is occupied by some other unit, we cannot move into it this tick.
        if (waypointIdx != unit.CurrentTileIdx)
        {
            var occupant = world.GetOccupant(waypointIdx);
            if (!occupant.IsNone && occupant != unit.Id)
            {
                HandleBlocked(world, unit);
                return;
            }
        }

        // 4. Free to move. Advance toward the waypoint, clamped to remaining distance.
        var delta = waypointPos - unit.Position;
        var distSq = delta.LengthSquared();
        var stepSq = unit.MoveSpeedPerTick * unit.MoveSpeedPerTick;

        FixedVector2 newPosition;
        bool reachedWaypoint;
        if (distSq <= stepSq)
        {
            newPosition = waypointPos;
            reachedWaypoint = true;
        }
        else
        {
            // Direction is delta / |delta|; step is direction * speed.
            // Combining: step = delta * (speed / |delta|).
            var dist = FixedMath.Sqrt(distSq);
            var scale = unit.MoveSpeedPerTick / dist;
            newPosition = unit.Position + new FixedVector2(delta.X * scale, delta.Y * scale);
            reachedWaypoint = false;
        }

        // 5. Facing follows velocity (the actual instantaneous velocity, before snap).
        if (delta.X.Raw != 0 || delta.Y.Raw != 0)
        {
            unit.Facing = FixedMath.Atan2(delta.Y, delta.X);
        }

        // 6. Tile-boundary crossing: update occupancy.
        int newTileIdx = TileIdxOf(newPosition);
        if (newTileIdx != unit.CurrentTileIdx)
        {
            world.ClearOccupant(unit.CurrentTileIdx, unit.Id);
            world.SetOccupant(newTileIdx, unit.Id);
            unit.CurrentTileIdx = newTileIdx;
        }
        unit.Position = newPosition;

        // 7. Reset wait counter — we moved.
        unit.WaitTicks = 0;
        unit.State = UnitState.Moving;

        // 8. Advance the path index when we hit a waypoint exactly.
        if (reachedWaypoint)
        {
            unit.PathIndex++;
            if (unit.PathIndex >= unit.Path.Count)
            {
                // Arrived at destination.
                unit.ClearPath();
                unit.State = UnitState.Idle;
                unit.DestinationTileIdx = -1;
            }
        }
    }

    private void ResolvePendingPath(World world, Unit unit)
    {
        int destIdx = unit.PendingDestinationIdx;
        unit.PendingDestinationIdx = -1;

        // Edge case: already at destination.
        if (unit.CurrentTileIdx == destIdx)
        {
            unit.ClearPath();
            unit.State = UnitState.Idle;
            unit.DestinationTileIdx = -1;
            return;
        }

        // Pathfind treating other units' current tiles as transient blockers, but the unit's
        // own start tile is always traversable (otherwise it could never escape itself).
        var selfId = unit.Id;
        int startIdx = unit.CurrentTileIdx;
        var worldRef = world;
        bool found = _pathfinder.FindPath(
            world.Map,
            startIdx,
            destIdx,
            unit.Path,
            isBlocked: idx =>
            {
                if (idx == startIdx) return false;
                var occ = worldRef.GetOccupant(idx);
                return !occ.IsNone && occ != selfId;
            });

        if (!found)
        {
            unit.ClearPath();
            unit.State = UnitState.Idle;
            unit.DestinationTileIdx = -1;
            return;
        }

        // Path is start-inclusive (Path[0] == startIdx). Set PathIndex to 1 so we head to the
        // first new tile, not back to where we already are.
        unit.PathIndex = (unit.Path.Count > 1) ? 1 : 0;
        unit.DestinationTileIdx = destIdx;
        unit.State = UnitState.Moving;
    }

    private void HandleBlocked(World world, Unit unit)
    {
        unit.State = UnitState.Waiting;
        unit.WaitTicks++;
        if (unit.WaitTicks < WaitTicksBeforeRepath) return;

        // Promote to a repath request.
        long currentTick = world.CurrentTick;
        if (currentTick - unit.RepathWindowStartTick >= RepathWindowLengthTicks)
        {
            unit.RepathWindowStartTick = currentTick;
            unit.RepathsInWindow = 0;
        }
        unit.RepathsInWindow++;
        unit.WaitTicks = 0;

        if (unit.RepathsInWindow > MaxRepathsInWindow)
        {
            // Give up.
            unit.ClearPath();
            unit.State = UnitState.Idle;
            unit.DestinationTileIdx = -1;
            unit.PendingDestinationIdx = -1;
            return;
        }

        // Otherwise, queue a fresh A* from current tile to existing destination.
        unit.PendingDestinationIdx = unit.DestinationTileIdx;
        unit.ClearPath();
    }

    // --- Coordinate helpers ---

    private static FixedVector2 TileCenter(int tileIdx)
    {
        int x = tileIdx % Grid.Width;
        int y = tileIdx / Grid.Width;
        // Center at (x + 0.5, y + 0.5).
        var half = Fixed64.FromRaw(Fixed64.Half);
        return new FixedVector2(Fixed64.FromInt(x) + half, Fixed64.FromInt(y) + half);
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
