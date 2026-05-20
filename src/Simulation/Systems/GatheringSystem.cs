using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Systems;

/// <summary>
/// Drives the villager state machine for resource gathering. Runs BEFORE
/// <see cref="MovementSystem"/> in the tick pipeline so any pending-path requests it sets are
/// consumed in the same tick.
///
/// State diagram:
///
///     Generic ──(GatherCommand)──┐
///                                ▼
///                     GoingToResource ──(adjacent &amp; res alive)──► Gathering
///                                ▲                                    │
///                                │           (carry full              │
///                                │            / res depleted)         │
///     (resource still alive,     │                 ▼                  │
///      carry empty after deposit)│                 GoingToDropOff     │
///                                │                         │           │
///                                │                         ▼ (adjacent)│
///                                └────── Depositing ◄──────┘
///
/// Iteration order is EntityId (the canonical order). Two villagers can never see each other
/// in a different order across replays, so resource contention resolves identically.
/// </summary>
public sealed class GatheringSystem
{
    // 20 ticks per +1 carry (1 carry/sec at 20 Hz).
    private const int GatherTicksPerCarry = 20;

    public void Tick(World world)
    {
        foreach (var unit in world.UnitsOrderedById())
        {
            switch (unit.Behavior)
            {
                case BehaviorKind.GoingToResource: HandleGoingToResource(world, unit); break;
                case BehaviorKind.Gathering: HandleGathering(world, unit); break;
                case BehaviorKind.GoingToDropOff: HandleGoingToDropOff(world, unit); break;
                case BehaviorKind.Depositing: HandleDepositing(world, unit); break;
                // BehaviorKind.Generic: no behavior; movement-system handles direct moves.
            }
        }
    }

    // --- Transitions ---

    private static void HandleGoingToResource(World world, Unit u)
    {
        if (!ResourceIsAlive(world, u.TargetEntityId, out var resTileX, out var resTileY))
        {
            // Resource we were heading to died (chopped by someone else, etc). If carrying
            // anything, head to drop-off; otherwise go idle.
            ResetToIdleOrDeposit(world, u);
            return;
        }

        if (IsAdjacentToTile(u.CurrentTileIdx, resTileX, resTileY))
        {
            // Arrived. Stop moving and start gathering.
            u.ClearPath();
            u.PendingDestinationIdx = -1;
            u.Behavior = BehaviorKind.Gathering;
            u.State = UnitState.Idle;
            u.GatherProgressTicks = 0;
        }
        // Otherwise: MovementSystem will continue advancing the path this same tick.
    }

    private static void HandleGathering(World world, Unit u)
    {
        // If the target resource died while we were standing on it, bail.
        if (!ResourceIsAlive(world, u.TargetEntityId, out var resTileX, out var resTileY))
        {
            // Try to find another same-kind resource nearby. If none, head home.
            if (FindNearbySameKind(world, u, searchRadius: 15, out var newTargetId))
            {
                u.TargetEntityId = newTargetId;
                u.Behavior = BehaviorKind.GoingToResource;
                u.PendingDestinationIdx = AdjacentTileTo(world, newTargetId);
                return;
            }
            ResetToIdleOrDeposit(world, u);
            return;
        }

        // If we drifted away (e.g. command-issued elsewhere), re-path.
        if (!IsAdjacentToTile(u.CurrentTileIdx, resTileX, resTileY))
        {
            u.Behavior = BehaviorKind.GoingToResource;
            u.PendingDestinationIdx = AdjacentTileTo(world, u.TargetEntityId);
            return;
        }

        // Tick gather progress.
        u.GatherProgressTicks++;
        if (u.GatherProgressTicks < GatherTicksPerCarry) return;
        u.GatherProgressTicks = 0;

        // Lift one unit of resource from the target into the carry.
        var (kind, lifted) = TakeOneUnitFromResource(world, u.TargetEntityId);
        if (lifted)
        {
            // First chunk taken sets the carry kind.
            if (u.Carried == CarryKind.None) u.Carried = kind;
            u.CarryAmount += Fixed64.OneValue;
        }

        // Carry full or resource depleted? Head to drop-off.
        bool resourceDead = !ResourceIsAlive(world, u.TargetEntityId, out _, out _);
        if (u.CarryAmount >= u.CarryCapacity || resourceDead)
        {
            BeginDropOff(world, u);
        }
    }

    private static void HandleGoingToDropOff(World world, Unit u)
    {
        // If our drop-off died, pick a new one.
        if (!IsValidDropOff(world, u.Owner, u.DropOffId, out var b))
        {
            if (!FindNearestDropOff(world, u, out var newId))
            {
                // No drop-off available — wander to a stop.
                ResetToIdleOrDeposit(world, u);
                return;
            }
            u.DropOffId = newId;
            u.PendingDestinationIdx = AdjacentTileToBuildingFootprint(world, newId);
            return;
        }

        if (b != null && IsAdjacentToFootprint(u.CurrentTileIdx, b))
        {
            u.ClearPath();
            u.PendingDestinationIdx = -1;
            u.Behavior = BehaviorKind.Depositing;
            u.State = UnitState.Idle;
        }
    }

    private static void HandleDepositing(World world, Unit u)
    {
        // Transfer all carry to player stockpile in a single tick. Then decide what's next.
        // Deposit the carry but keep u.Carried set so the re-targeting logic below can use it
        // as a filter key. We clear u.Carried only once we know what the unit will do next.
        if (u.CarryAmount.Raw > 0)
        {
            var player = world.GetPlayer(u.Owner);
            switch (u.Carried)
            {
                case CarryKind.Wood: player.Wood += u.CarryAmount; break;
                case CarryKind.Food: player.Food += u.CarryAmount; break;
                case CarryKind.Gold: player.Gold += u.CarryAmount; break;
            }
            u.CarryAmount = Fixed64.Zero;
            // NOTE: intentionally NOT clearing u.Carried here — FindNearbySameKind below reads
            // it to know what kind of resource to find. It will be cleared via ClearPath/idle or
            // left set when the unit immediately starts another gather trip (correct, since the
            // unit is about to gather the same resource type again).
        }

        // If the original resource is still alive, go back to it.
        if (ResourceIsAlive(world, u.TargetEntityId, out _, out _))
        {
            u.Behavior = BehaviorKind.GoingToResource;
            u.PendingDestinationIdx = AdjacentTileTo(world, u.TargetEntityId);
            return;
        }

        // Else: try to find a same-kind resource nearby.
        if (FindNearbySameKind(world, u, searchRadius: 15, out var newTargetId))
        {
            u.TargetEntityId = newTargetId;
            u.Behavior = BehaviorKind.GoingToResource;
            u.PendingDestinationIdx = AdjacentTileTo(world, newTargetId);
            return;
        }

        // Nothing to gather — go idle and clear the carry kind.
        u.Carried = CarryKind.None;
        u.Behavior = BehaviorKind.Generic;
        u.TargetEntityId = EntityId.None;
    }

    private static void BeginDropOff(World world, Unit u)
    {
        if (!FindNearestDropOff(world, u, out var dropId))
        {
            // No drop-off — sit on the carry. Future villager could deposit when a TC exists.
            u.Behavior = BehaviorKind.Generic;
            return;
        }
        u.DropOffId = dropId;
        u.Behavior = BehaviorKind.GoingToDropOff;
        u.PendingDestinationIdx = AdjacentTileToBuildingFootprint(world, dropId);
    }

    private static void ResetToIdleOrDeposit(World world, Unit u)
    {
        if (u.CarryAmount.Raw > 0)
        {
            BeginDropOff(world, u);
            return;
        }
        u.Behavior = BehaviorKind.Generic;
        u.TargetEntityId = EntityId.None;
        u.DropOffId = EntityId.None;
        u.State = UnitState.Idle;
        u.ClearPath();
    }

    // --- Resource access ---

    private static bool ResourceIsAlive(World world, EntityId id, out int tileX, out int tileY)
    {
        tileX = 0; tileY = 0;
        if (id.IsNone) return false;
        if (!world.TryGetEntity(id, out var obj)) return false;
        switch (obj)
        {
            case Tree t when !t.IsDepleted: tileX = t.TileX; tileY = t.TileY; return true;
            case BerryBush b when !b.IsDepleted: tileX = b.TileX; tileY = b.TileY; return true;
            default: return false;
        }
    }

    private static (CarryKind kind, bool lifted) TakeOneUnitFromResource(World world, EntityId id)
    {
        if (!world.TryGetEntity(id, out var obj)) return (CarryKind.None, false);
        switch (obj)
        {
            case Tree t when !t.IsDepleted:
                t.WoodRemaining -= Fixed64.OneValue;
                if (t.IsDepleted)
                    world.ClearOccupant(t.TileY * Grid.Width + t.TileX, t.Id);
                return (CarryKind.Wood, true);
            case BerryBush b when !b.IsDepleted:
                b.FoodRemaining -= Fixed64.OneValue;
                if (b.IsDepleted)
                    world.ClearOccupant(b.TileY * Grid.Width + b.TileX, b.Id);
                return (CarryKind.Food, true);
            default:
                return (CarryKind.None, false);
        }
    }

    private static bool IsValidDropOff(World world, PlayerId owner, EntityId id, out Building? building)
    {
        building = null;
        if (id.IsNone) return false;
        if (!world.TryGetEntity(id, out var obj)) return false;
        if (obj is Building b && !b.IsDestroyed && b.Owner == owner && b.Type == BuildingTypeId.TownHall)
        {
            building = b;
            return true;
        }
        return false;
    }

    private static bool FindNearestDropOff(World world, Unit u, out EntityId id)
    {
        id = EntityId.None;
        int bestDistSq = int.MaxValue;
        int ux = u.CurrentTileIdx % Grid.Width;
        int uy = u.CurrentTileIdx / Grid.Width;
        foreach (var b in world.BuildingsOrderedById())
        {
            if (b.IsDestroyed) continue;
            if (b.Owner != u.Owner) continue;
            if (b.Type != BuildingTypeId.TownHall) continue;
            int cx = b.TileX + b.FootprintW / 2;
            int cy = b.TileY + b.FootprintH / 2;
            int dx = cx - ux, dy = cy - uy;
            int dSq = dx * dx + dy * dy;
            if (dSq < bestDistSq) { bestDistSq = dSq; id = b.Id; }
        }
        return !id.IsNone;
    }

    private static bool FindNearbySameKind(World world, Unit u, int searchRadius, out EntityId id)
    {
        id = EntityId.None;
        var targetKind = u.Carried; // what we just deposited — go find more of the same
        if (targetKind == CarryKind.None) return false;

        int bestDistSq = int.MaxValue;
        int ux = u.CurrentTileIdx % Grid.Width;
        int uy = u.CurrentTileIdx / Grid.Width;

        if (targetKind == CarryKind.Wood)
        {
            foreach (var t in world.TreesOrderedById())
            {
                if (t.IsDepleted) continue;
                int dx = t.TileX - ux, dy = t.TileY - uy;
                int dSq = dx * dx + dy * dy;
                if (dSq <= searchRadius * searchRadius && dSq < bestDistSq) { bestDistSq = dSq; id = t.Id; }
            }
        }
        else if (targetKind == CarryKind.Food)
        {
            foreach (var b in world.BushesOrderedById())
            {
                if (b.IsDepleted) continue;
                int dx = b.TileX - ux, dy = b.TileY - uy;
                int dSq = dx * dx + dy * dy;
                if (dSq <= searchRadius * searchRadius && dSq < bestDistSq) { bestDistSq = dSq; id = b.Id; }
            }
        }
        return !id.IsNone;
    }

    // --- Tile geometry ---

    private static bool IsAdjacentToTile(int unitTileIdx, int rTx, int rTy)
    {
        int ux = unitTileIdx % Grid.Width;
        int uy = unitTileIdx / Grid.Width;
        return System.Math.Abs(ux - rTx) <= 1 && System.Math.Abs(uy - rTy) <= 1 && (ux != rTx || uy != rTy);
    }

    private static bool IsAdjacentToFootprint(int unitTileIdx, Building b)
    {
        int ux = unitTileIdx % Grid.Width;
        int uy = unitTileIdx / Grid.Width;
        if (ux < b.TileX - 1 || ux > b.TileX + b.FootprintW) return false;
        if (uy < b.TileY - 1 || uy > b.TileY + b.FootprintH) return false;
        // Inside the footprint doesn't count as adjacent; must be on the rim.
        bool insideX = ux >= b.TileX && ux < b.TileX + b.FootprintW;
        bool insideY = uy >= b.TileY && uy < b.TileY + b.FootprintH;
        return !(insideX && insideY);
    }

    /// <summary>
    /// Return a passable tile adjacent to the given resource. Walks the 8 neighbors in a fixed
    /// order (same as the spiral in CommandProcessor) and returns the first passable + unoccupied
    /// one. Falls back to the resource's tile if every neighbor is blocked (movement will then
    /// fail to path and the unit will give up — that's fine).
    /// </summary>
    private static int AdjacentTileTo(World world, EntityId resourceId)
    {
        if (!world.TryGetEntity(resourceId, out var obj)) return -1;
        int rTx, rTy;
        switch (obj)
        {
            case Tree t: rTx = t.TileX; rTy = t.TileY; break;
            case BerryBush b: rTx = b.TileX; rTy = b.TileY; break;
            default: return -1;
        }
        return FirstPassableNeighbor(world, rTx, rTy);
    }

    private static int AdjacentTileToBuildingFootprint(World world, EntityId buildingId)
    {
        if (!world.TryGetEntity(buildingId, out var obj) || obj is not Building b) return -1;
        // Walk the rim of the footprint; return the first passable neighbor.
        for (int dy = -1; dy <= b.FootprintH; dy++)
        {
            for (int dx = -1; dx <= b.FootprintW; dx++)
            {
                bool insideX = dx >= 0 && dx < b.FootprintW;
                bool insideY = dy >= 0 && dy < b.FootprintH;
                if (insideX && insideY) continue;
                int nx = b.TileX + dx, ny = b.TileY + dy;
                if ((uint)nx >= Grid.Width || (uint)ny >= Grid.Height) continue;
                if (!world.Map.IsPassable(nx, ny)) continue;
                int idx = ny * Grid.Width + nx;
                var occ = world.GetOccupant(idx);
                if (!occ.IsNone) continue;
                return idx;
            }
        }
        return -1;
    }

    private static int FirstPassableNeighbor(World world, int cx, int cy)
    {
        // Cardinal first, then diagonal — same order as movement neighborhood scan.
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
            var occ = world.GetOccupant(idx);
            if (!occ.IsNone) continue;
            return idx;
        }
        return -1;
    }
}
