using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Systems;

/// <summary>
/// M4 combat layer. Runs alongside <see cref="GatheringSystem"/> in the behavior phase of the
/// tick pipeline (before MovementSystem). Handles two behavior states:
///
///   Pursuing  : moving toward a hostile TargetEntityId. When the attacker enters melee
///               range (Chebyshev 1 from the target's tile / building rim), it transitions
///               to Attacking.
///   Attacking : in range, ticking AttackCooldown. Each cooldown expiry deducts
///               attacker damage minus target armor from target HP. Death (HP &lt;= 0)
///               removes the target via World.RemoveEntity and frees the attacker.
///
/// Damage is fully deterministic — no random rolls. Bonus damage (e.g., spear vs cavalry,
/// ram vs buildings) is queried via the UnitStats helpers per attacker/target pair.
///
/// Death side-effects ripple through the rest of the tick: occupancy clears, pop count
/// decrements (for unit deaths), the building footprint reopens (for building deaths).
/// </summary>
public sealed class CombatSystem
{
    public void Tick(World world)
    {
        // Snapshot the iteration set: combat can remove entities mid-iteration, and
        // SortedDictionary doesn't tolerate that.
        var snapshot = new List<EntityId>();
        foreach (var u in world.UnitsOrderedById()) snapshot.Add(u.Id);
        foreach (var id in snapshot)
        {
            if (!world.TryGetEntity(id, out var obj) || obj is not Unit u) continue;
            if (u.Behavior == BehaviorKind.Pursuing) HandlePursuing(world, u);
            else if (u.Behavior == BehaviorKind.Attacking) HandleAttacking(world, u);
        }
    }

    private static void HandlePursuing(World world, Unit u)
    {
        if (!ResolveTarget(world, u.TargetEntityId, out var tTileIdx, out var tFootprint))
        {
            // Target gone — return to idle.
            u.Behavior = BehaviorKind.Generic;
            u.TargetEntityId = EntityId.None;
            u.State = UnitState.Idle;
            u.ClearPath();
            return;
        }

        if (InMeleeRange(u.CurrentTileIdx, tTileIdx, tFootprint))
        {
            u.ClearPath();
            u.PendingDestinationIdx = -1;
            u.Behavior = BehaviorKind.Attacking;
            u.State = UnitState.Idle;
            return;
        }

        // Out of range — keep moving toward an adjacent tile of the target. Recompute the
        // approach tile each behavior tick so a moving target stays reachable.
        int approachIdx = FirstApproachTile(world, tTileIdx, tFootprint);
        if (approachIdx >= 0) u.PendingDestinationIdx = approachIdx;
    }

    private static void HandleAttacking(World world, Unit u)
    {
        if (!ResolveTarget(world, u.TargetEntityId, out var tTileIdx, out var tFootprint))
        {
            u.Behavior = BehaviorKind.Generic;
            u.TargetEntityId = EntityId.None;
            u.State = UnitState.Idle;
            return;
        }

        if (!InMeleeRange(u.CurrentTileIdx, tTileIdx, tFootprint))
        {
            // Target moved away — return to pursuit.
            u.Behavior = BehaviorKind.Pursuing;
            return;
        }

        // Face the target.
        int ux = u.CurrentTileIdx % Grid.Width;
        int uy = u.CurrentTileIdx / Grid.Width;
        int tx = tTileIdx % Grid.Width;
        int ty = tTileIdx / Grid.Width;
        var dx = Fixed64.FromInt(tx - ux);
        var dy = Fixed64.FromInt(ty - uy);
        if (dx.Raw != 0 || dy.Raw != 0) u.Facing = FixedMath.Atan2(dy, dx);

        // Cooldown.
        u.AttackCooldownTicks++;
        int cdMax = UnitStats.AttackCooldownTicks(u.UnitTypeId);
        if (u.AttackCooldownTicks < cdMax) return;
        u.AttackCooldownTicks = 0;

        // Apply damage.
        int rawDamage = UnitStats.MeleeDamage(u.UnitTypeId);
        int armor = ArmorOf(world, u.TargetEntityId);
        int damage = System.Math.Max(1, rawDamage - armor);
        ApplyDamage(world, u.TargetEntityId, damage);
    }

    // --- Helpers ---

    private static bool ResolveTarget(World world, EntityId id, out int tileIdx, out (int tx, int ty, int w, int h)? footprint)
    {
        tileIdx = 0;
        footprint = null;
        if (id.IsNone) return false;
        if (!world.TryGetEntity(id, out var obj)) return false;
        switch (obj)
        {
            case Unit u when u.HpCurrent.Raw > 0:
                tileIdx = u.CurrentTileIdx;
                return true;
            case Building b when !b.IsDestroyed:
                tileIdx = (b.TileY + b.FootprintH / 2) * Grid.Width + (b.TileX + b.FootprintW / 2);
                footprint = (b.TileX, b.TileY, b.FootprintW, b.FootprintH);
                return true;
            default:
                return false;
        }
    }

    private static bool InMeleeRange(int attackerTileIdx, int targetTileIdx, (int tx, int ty, int w, int h)? footprint)
    {
        int ax = attackerTileIdx % Grid.Width;
        int ay = attackerTileIdx / Grid.Width;
        if (footprint is (int tx, int ty, int w, int h))
        {
            // Adjacent to the footprint rim (Chebyshev 1 to any rim tile, inclusive).
            if (ax < tx - 1 || ax > tx + w) return false;
            if (ay < ty - 1 || ay > ty + h) return false;
            bool insideX = ax >= tx && ax < tx + w;
            bool insideY = ay >= ty && ay < ty + h;
            return !(insideX && insideY);
        }
        int rTx = targetTileIdx % Grid.Width;
        int rTy = targetTileIdx / Grid.Width;
        return System.Math.Abs(ax - rTx) <= 1 && System.Math.Abs(ay - rTy) <= 1;
    }

    private static int FirstApproachTile(World world, int targetTileIdx, (int tx, int ty, int w, int h)? footprint)
    {
        int cx, cy;
        if (footprint is (int tx, int ty, int w, int h))
        {
            for (int dy = -1; dy <= h; dy++)
            {
                for (int dx = -1; dx <= w; dx++)
                {
                    bool inX = dx >= 0 && dx < w, inY = dy >= 0 && dy < h;
                    if (inX && inY) continue;
                    int nx = tx + dx, ny = ty + dy;
                    if ((uint)nx >= Grid.Width || (uint)ny >= Grid.Height) continue;
                    if (!world.Map.IsPassable(nx, ny)) continue;
                    int idx = ny * Grid.Width + nx;
                    if (!world.GetOccupant(idx).IsNone) continue;
                    return idx;
                }
            }
            return -1;
        }

        cx = targetTileIdx % Grid.Width;
        cy = targetTileIdx / Grid.Width;
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
        return -1;
    }

    private static int ArmorOf(World world, EntityId targetId)
    {
        if (!world.TryGetEntity(targetId, out var obj)) return 0;
        return obj switch
        {
            Unit u => UnitStats.MeleeArmor(u.UnitTypeId),
            Building => 3, // M4 placeholder: all buildings have melee armor 3
            _ => 0,
        };
    }

    private static void ApplyDamage(World world, EntityId targetId, int damage)
    {
        if (!world.TryGetEntity(targetId, out var obj)) return;
        var dmg = Fixed64.FromInt(damage);
        switch (obj)
        {
            case Unit u:
                u.HpCurrent -= dmg;
                if (u.HpCurrent.Raw <= 0) world.RemoveEntity(u.Id);
                break;
            case Building b:
                b.HpCurrent -= dmg;
                if (b.HpCurrent.Raw <= 0) world.RemoveEntity(b.Id);
                break;
        }
    }
}

/// <summary>Per-unit-type combat stats. Hardcoded for M4; moves to JSON loader in M5.</summary>
public static class UnitStats
{
    public static int MeleeDamage(int unitTypeId) => unitTypeId switch { 1 => 3, 2 => 4, _ => 1 };
    public static int MeleeArmor(int unitTypeId) => unitTypeId switch { 1 => 0, 2 => 0, _ => 0 };
    /// <summary>Attack interval in ticks. 40 = 2s at 20 Hz; matches the 2.0 attackSpeed in units.json.</summary>
    public static int AttackCooldownTicks(int unitTypeId) => unitTypeId switch { 1 => 40, 2 => 40, _ => 40 };
}
