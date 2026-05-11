using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Entities;

/// <summary>High-level FSM state for a unit. M1 covers Idle/Moving/Waiting only.</summary>
public enum UnitState : byte
{
    Idle = 0,
    Moving = 1,
    Waiting = 2,    // next tile blocked; counting toward repath threshold
}

/// <summary>
/// Mobile entity. M1 shape: identity, owner, position, facing, hp, plus movement state
/// (current path, pending destination, collision counters). Behavior systems and unit-type
/// data arrive in M3+.
///
/// Position is in tile-space: (1.0, 0.0) is one tile east of the origin. <see cref="CurrentTileIdx"/>
/// is the tile the unit's center is currently in (computed when the unit crosses tile boundaries),
/// and is the cell used in the world's occupancy table.
/// </summary>
public sealed class Unit : IHashable
{
    public EntityId Id { get; }
    public PlayerId Owner { get; set; }
    public FixedVector2 Position { get; set; }
    public Fixed64 Facing { get; set; }
    public Fixed64 HpCurrent { get; set; }
    public Fixed64 HpMax { get; set; }

    /// <summary>Per-tick movement speed, in tile units. Set per unit type; default = villager pace.</summary>
    public Fixed64 MoveSpeedPerTick { get; set; } = Fixed64.FromRaw(2949);  // ~0.045 tiles/tick at 20Hz ~ 0.9 tiles/sec

    /// <summary>Tile index (y * Width + x) that this unit currently occupies in the grid.</summary>
    public int CurrentTileIdx { get; set; }

    public UnitState State { get; set; } = UnitState.Idle;

    /// <summary>Destination tile index of the active move order, or -1 if none.</summary>
    public int DestinationTileIdx { get; set; } = -1;

    /// <summary>
    /// New destination requested via MoveCommand this tick but not yet pathed. The MovementSystem
    /// consumes this on its next pass, runs A*, and resets it to -1. Use -1 for "no request".
    /// </summary>
    public int PendingDestinationIdx { get; set; } = -1;

    /// <summary>Sequence of tile indices from start to destination. Path[PathIndex] is the next waypoint.</summary>
    public List<int> Path { get; } = new();
    public int PathIndex { get; set; }

    /// <summary>Ticks the unit has been blocked at the current waypoint. Threshold = 5 -> repath.</summary>
    public int WaitTicks { get; set; }

    /// <summary>Repath count within the current 100-tick window. Threshold = 3 -> Idle / give up.</summary>
    public int RepathsInWindow { get; set; }
    public long RepathWindowStartTick { get; set; }

    public Unit(EntityId id)
    {
        Id = id;
    }

    public bool HasPath => Path.Count > 0 && PathIndex < Path.Count;

    public void ClearPath()
    {
        Path.Clear();
        PathIndex = 0;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix(Owner.Value);
        hash.Mix(Position.X.Raw);
        hash.Mix(Position.Y.Raw);
        hash.Mix(Facing.Raw);
        hash.Mix(HpCurrent.Raw);
        hash.Mix(HpMax.Raw);
        hash.Mix(MoveSpeedPerTick.Raw);
        hash.Mix((long)CurrentTileIdx);
        hash.MixByte((byte)State);
        hash.Mix((long)DestinationTileIdx);
        hash.Mix((long)PendingDestinationIdx);
        hash.Mix((long)PathIndex);
        hash.Mix((long)Path.Count);
        for (int i = 0; i < Path.Count; i++) hash.Mix((long)Path[i]);
        hash.Mix((long)WaitTicks);
        hash.Mix((long)RepathsInWindow);
        hash.Mix(RepathWindowStartTick);
    }
}
