using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;
using WarOfKings.Simulation.Systems;

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
                case GatherCommand gc: ApplyGatherCommand(world, gc); break;
                case TrainCommand tc: ApplyTrainCommand(world, tc); break;
                case AttackCommand ac: ApplyAttackCommand(world, ac); break;
                // Build arrives in a later milestone.
            }
        }
    }

    private static void ApplyAttackCommand(World world, AttackCommand ac)
    {
        // Validate target exists.
        if (!world.TryGetEntity(ac.Target, out var targetObj)) return;
        switch (targetObj)
        {
            case Entities.Unit ut when ut.HpCurrent.Raw > 0:
            case Entities.Building bt when !bt.IsDestroyed:
                break;
            default:
                return;
        }

        var orderedAttackers = new List<EntityId>(ac.Attackers);
        orderedAttackers.Sort();
        foreach (var unitId in orderedAttackers)
        {
            if (!world.TryGetEntity(unitId, out var entity) || entity is not Entities.Unit u) continue;
            if (u.Owner != ac.Player) continue;
            u.TargetEntityId = ac.Target;
            u.Behavior = Entities.BehaviorKind.Pursuing;
            u.PendingDestinationIdx = -1; // CombatSystem will set the approach tile this tick
            u.AttackCooldownTicks = System.Math.Max(0, Systems.UnitStats.AttackCooldownTicks(u.UnitTypeId) - 1);
            u.WaitTicks = 0;
            u.RepathsInWindow = 0;
            u.RepathWindowStartTick = world.CurrentTick;
        }
    }

    private static void ApplyTrainCommand(World world, TrainCommand tc)
    {
        if (!world.TryGetEntity(tc.ProductionBuilding, out var obj) || obj is not Entities.Building b) return;
        if (b.IsDestroyed) return;
        if (b.Owner != tc.Player) return;
        if (b.ProductionQueue.Count >= Entities.Building.MaxQueueDepth) return;

        // Resource check.
        var player = world.GetPlayer(tc.Player);
        var costFood = Fixed64.FromInt(ProductionSystem.CostFood(tc.UnitTypeId));
        var costWood = Fixed64.FromInt(ProductionSystem.CostWood(tc.UnitTypeId));
        var costGold = Fixed64.FromInt(ProductionSystem.CostGold(tc.UnitTypeId));
        if (player.Food < costFood || player.Wood < costWood || player.Gold < costGold) return;

        player.Food -= costFood;
        player.Wood -= costWood;
        player.Gold -= costGold;
        b.ProductionQueue.Add(tc.UnitTypeId);
    }

    private static void ApplyGatherCommand(World world, GatherCommand gc)
    {
        // Validate target resource exists and is alive.
        if (!world.TryGetEntity(gc.ResourceNode, out var resObj)) return;
        int resX, resY;
        switch (resObj)
        {
            case Entities.Tree t when !t.IsDepleted: resX = t.TileX; resY = t.TileY; break;
            case Entities.BerryBush b when !b.IsDepleted: resX = b.TileX; resY = b.TileY; break;
            default: return;
        }

        // Spread gatherers across the 8 neighbor tiles of the resource using the same spiral
        // used by MoveCommand. Without this, all N villagers are assigned the same tile,
        // collide on arrival, and most exhaust their repath budget and go idle.
        var assigned = new HashSet<int>();

        var ordered = new List<EntityId>(gc.Gatherers);
        ordered.Sort();
        foreach (var unitId in ordered)
        {
            if (!world.TryGetEntity(unitId, out var entity) || entity is not Entities.Unit u) continue;
            if (u.Owner != gc.Player) continue;

            // Find the nearest unassigned, passable, unoccupied neighbor in canonical offset order.
            int chosen = -1;
            var offsets = new (int dx, int dy)[] {
                (1,0),(-1,0),(0,1),(0,-1),
                (1,1),(-1,1),(-1,-1),(1,-1),
            };
            foreach (var (dx, dy) in offsets)
            {
                int nx = resX + dx, ny = resY + dy;
                if ((uint)nx >= Grid.Width || (uint)ny >= Grid.Height) continue;
                if (!world.Map.IsPassable(nx, ny)) continue;
                int idx = ny * Grid.Width + nx;
                if (!world.GetOccupant(idx).IsNone) continue;
                if (!assigned.Add(idx)) continue;
                chosen = idx;
                break;
            }
            // If all 8 neighbors are already claimed or blocked, fall back to the first
            // passable neighbor (GatheringSystem will sort out adjacency on arrival).
            if (chosen < 0)
            {
                int? fallback = FirstPassableAdjacent(world, resX, resY);
                if (fallback is not int fb) continue;
                chosen = fb;
            }

            u.TargetEntityId = gc.ResourceNode;
            u.Behavior = Entities.BehaviorKind.GoingToResource;
            u.PendingDestinationIdx = chosen;
            u.ClearPath();
            u.WaitTicks = 0;
            u.RepathsInWindow = 0;
            u.RepathWindowStartTick = world.CurrentTick;
            u.State = Entities.UnitState.Moving;
        }
    }

    private static int? FirstPassableAdjacent(World world, int cx, int cy)
    {
        var offsets = new (int dx, int dy)[] {
            (1,0),(-1,0),(0,1),(0,-1),
            (1,1),(-1,1),(-1,-1),(1,-1),
        };
        foreach (var (dx, dy) in offsets)
        {
            int nx = cx + dx, ny = cy + dy;
            if ((uint)nx >= Grid.Width || (uint)ny >= Grid.Height) continue;
            if (!world.Map.IsPassable(nx, ny)) continue;
            int idx = ny * Grid.Width + nx;
            if (!world.GetOccupant(idx).IsNone) continue;
            return idx;
        }
        return null;
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
