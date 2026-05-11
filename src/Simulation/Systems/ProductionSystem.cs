using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Systems;

/// <summary>
/// Drives the training queue on every Building. Runs once per tick after
/// <see cref="MovementSystem"/>: by then any unit births spawned this tick won't fight with
/// the in-flight movement updates from existing units.
///
/// M3 supports one in-progress trainee per building (the head of <see cref="Building.ProductionQueue"/>);
/// trained units spawn on the first passable tile around the footprint, in the canonical
/// neighbor order. Population cap is enforced at spawn time — if the cap is full, the trainee
/// stays in the queue and progress freezes until a unit dies.
/// </summary>
public sealed class ProductionSystem
{
    public void Tick(World world)
    {
        foreach (var b in world.BuildingsOrderedById())
        {
            if (b.IsDestroyed) continue;
            if (b.ProductionQueue.Count == 0) continue;

            int unitTypeId = b.ProductionQueue[0];
            int trainTicks = TrainTicksFor(unitTypeId);
            if (trainTicks <= 0) { b.ProductionQueue.RemoveAt(0); b.ProductionProgressTicks = 0; continue; }

            // Honor pop cap: if there's no room, leave progress where it is.
            int popCost = PopCostFor(unitTypeId);
            var player = world.GetPlayer(b.Owner);
            if (player.PopCurrent + popCost > player.PopCap) continue;

            b.ProductionProgressTicks++;
            if (b.ProductionProgressTicks < trainTicks) continue;

            // Spawn.
            int? spawnIdx = FirstPassableNeighbor(world, b);
            if (spawnIdx is null) continue; // no room yet; will retry next tick

            int sx = spawnIdx.Value % Grid.Width;
            int sy = spawnIdx.Value / Grid.Width;
            // Spawn at tile center so the unit fits the occupancy + visual model.
            var half = Fixed64.FromRaw(Fixed64.Half);
            var pos = new FixedVector2(Fixed64.FromInt(sx) + half, Fixed64.FromInt(sy) + half);
            var u = world.CreateUnit(b.Owner, pos);
            // Set unit-type-specific stats (M3: only villager from TC; M4 will add militia).
            ApplyUnitTypeStats(u, unitTypeId);
            player.PopCurrent += popCost;

            b.ProductionQueue.RemoveAt(0);
            b.ProductionProgressTicks = 0;
        }
    }

    // M3 hardcoded stats. Will move to assets/data/units.json + a loader in a later milestone.
    public static int CostFood(int unitTypeId) => unitTypeId switch { 1 => 50, 2 => 60, _ => 0 };
    public static int CostWood(int unitTypeId) => 0;
    public static int CostGold(int unitTypeId) => unitTypeId switch { 2 => 20, _ => 0 };
    public static int TrainTicksFor(int unitTypeId) => unitTypeId switch { 1 => 500, 2 => 420, _ => 0 };
    public static int PopCostFor(int unitTypeId) => unitTypeId switch { 1 or 2 => 1, _ => 1 };

    private static void ApplyUnitTypeStats(Unit u, int unitTypeId)
    {
        switch (unitTypeId)
        {
            case 1: // Villager — already the default Unit defaults; nothing extra to set in M3.
                break;
            case 2: // Militia (M4 placeholder).
                u.HpCurrent = Fixed64.FromInt(40);
                u.HpMax = Fixed64.FromInt(40);
                break;
        }
    }

    private static int? FirstPassableNeighbor(World world, Building b)
    {
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
                if (!world.GetOccupant(idx).IsNone) continue;
                return idx;
            }
        }
        return null;
    }
}
